using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private GameObject hitboxObject;
    [SerializeField] private HitboxController hitboxController; // hitboxObject와 같은 오브젝트

    [SerializeField] private PlayerState playerState;
    [SerializeField] private Animator animator;
    [SerializeField] private Facing playerFacing;
    [SerializeField] private Transform attackPivot; // Hitbox(및 추후 검기 이펙트)를 클릭 방향으로 회전시키는 피벗
    [SerializeField] private PlayerHotbar hotbar; // 선택한 도구를 공격에 반영
    [SerializeField] private InventoryUI inventoryUI; // 아이템을 들고 있을 때의 월드 클릭은 던지기이므로 공격하지 않음
    [SerializeField] private int bareHandDamage = 5; // 도구가 아닌 아이템을 들었거나 빈 손일 때의 피해량

    [Tooltip("공격 타이밍·쿨다운·캔슬 구간")]
    [SerializeField] private PlayerCombatSetting setting;
    [SerializeField] private PlayerInputBuffer inputBuffer;
    [SerializeField] private PlayerMovement playerMovement; // 경직 중 이동 입력 확인

    [SerializeField] private float cameraYAngle = 45f; // PlayerMovement/Billboard의 값과 반드시 일치해야함
    private Vector3 cameraRight;
    private Vector3 cameraForwardFlat;

    private Vector2 pointerScreenPos;

    // 누르고 있으면 공격 가능해지는 순간마다 자동으로 다음 공격 (SendMessages는 누른 순간에만 OnAttack을 보내므로 직접 확인)
    private InputAction attackAction;
    private bool holdAttack; // UI 밖에서 누르기 시작했을 때만 켜짐 (UI 드래그 중 공격 방지)

    private enum AttackPhase { None, Windup, Attack, Recover }
    private AttackPhase attackPhase = AttackPhase.None;
    private float phaseTime;
    private float lastAttackTime = -999f; // 시작 시 바로 사용 가능하도록 충분히 작은 값

    // 판정이 켜지고 꺼지는 시점 (휘두르기 이펙트 등이 구독)
    public event Action AttackStarted;
    public event Action AttackEnded;

    // 이번 공격의 속성 (들고 있는 도구에서 정해지며, 휘두르기 이펙트가 모양을 고르는 데 사용)
    public ElementType CurrentElement { get; private set; }

    // 판정이 켜져 있는 시간 (휘두르기 이펙트 재생 길이)
    public float ActiveDuration => setting.attackActive;

    private Rigidbody rb;
    // 도구마다 공격 범위를 바꾸기 위한 히트박스 원래 크기
    private BoxCollider hitboxBox;
    private Vector3 hitboxBaseSize;
    private Vector3 hitboxBaseCenter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        hitboxBox = hitboxObject.GetComponent<BoxCollider>();
        if (hitboxBox != null)
        {
            hitboxBaseSize = hitboxBox.size;
            hitboxBaseCenter = hitboxBox.center;
        }
        hitboxObject.SetActive(false);

        PlayerInput playerInput = GetComponentInParent<PlayerInput>();
        if (playerInput != null)
        {
            attackAction = playerInput.actions.FindAction("Attack");
        }

        Quaternion cameraRotation = Quaternion.Euler(0, cameraYAngle, 0);
        cameraRight = cameraRotation * Vector3.right;
        cameraForwardFlat = cameraRotation * Vector3.forward;

        // 공격 도중 넉백되면 Update가 멈추므로 히트박스가 켜진 채 남지 않도록 공격을 취소
        if (TryGetComponent<Knockback>(out Knockback knockback))
        {
            knockback.Started += CancelAttack;
        }

        // 공격 도중 사망해도 같은 이유로 공격을 취소
        if (TryGetComponent<Health>(out Health health))
        {
            health.Died += CancelAttack;
        }

        // 공격 도중 기절해도 같은 이유로 공격을 취소
        playerState.StunStarted += CancelAttack;
    }

    // 진행 중인 공격을 끊음 (넉백·사망·기절, 공격 중 회피). 상태 전환은 호출한 쪽이 처리
    public void CancelAttack()
    {
        if (attackPhase == AttackPhase.None) return;

        attackPhase = AttackPhase.None;
        hitboxObject.SetActive(false);
        animator.ResetTrigger("Attack");
        AttackEnded?.Invoke();
    }

    private void FixedUpdate()
    {
        // Attack일 때만 실행 (다른 클래스와 배타적)
        if (playerState.CurrentState != PlayerActionState.Attack) return;

        // 몸체 마찰이 0이라 이동 속도가 남아 미끄러지므로 공격 중에는 제자리에 고정
        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(0f, velocity.y, 0f);
    }

    private void Update()
    {
        // 선입력된 공격은 실행할 수 있게 되는 순간 실행 (회피·넉백 종료 직후, 경직 캔슬 구간 진입 시 등)
        if (inputBuffer.Has(BufferedAction.Attack) && CanStartAttack())
        {
            inputBuffer.Clear();
            StartAttack();
            return;
        }

        // 공격 버튼을 누르고 있는 동안에는 공격할 수 있게 되는 순간 다음 공격을 실행
        if (holdAttack)
        {
            if (attackAction == null || !attackAction.IsPressed())
            {
                holdAttack = false;
            }
            else if (CanStartAttack() && !PointerUtil.IsOverUI(pointerScreenPos))
            {
                inputBuffer.Clear();
                StartAttack();
                return;
            }
        }

        // Attack일 때만 실행 (다른 클래스와 배타적)
        if (playerState.CurrentState != PlayerActionState.Attack) return;

        phaseTime += Time.deltaTime;

        switch (attackPhase)
        {
            case AttackPhase.Windup:
                if (phaseTime >= setting.attackWindup)
                {
                    attackPhase = AttackPhase.Attack;
                    phaseTime = 0f;

                    hitboxController.ResetHitTargets();
                    hitboxObject.SetActive(true);
                    AttackStarted?.Invoke();
                }
                break;

            case AttackPhase.Attack:
                if (phaseTime >= setting.attackActive)
                {
                    attackPhase = AttackPhase.Recover;
                    phaseTime = 0f;

                    hitboxObject.SetActive(false);
                    AttackEnded?.Invoke();
                }    
                break;

            case AttackPhase.Recover:
                // 경직이 끝났거나, 캔슬 구간에서 이동 입력이 들어오면 바로 이동 가능 상태로
                bool moveCancel = phaseTime >= setting.recoverMoveCancelAfter && playerMovement.HasMoveInput;
                if (phaseTime >= setting.attackRecover || moveCancel)
                {
                    attackPhase = AttackPhase.None;
                    playerState.SetState(PlayerActionState.Normal);
                }
                break;
        }
    }

    // 평소 상태이거나, 공격 경직의 캔슬 구간이면 새 공격 가능 (쿨다운은 공통)
    private bool CanStartAttack()
    {
        if (Time.time < lastAttackTime + setting.attackCooldown) return false;
        if (playerState.CanAct) return true;

        return playerState.CurrentState == PlayerActionState.Attack
            && attackPhase == AttackPhase.Recover
            && phaseTime >= setting.recoverAttackCancelAfter;
    }

    void OnPoint(InputValue value)
    {
        pointerScreenPos = value.Get<Vector2>();
    }

    void OnAttack(InputValue value)
    {
        // UI(인벤토리 칸 등) 위를 클릭한 경우 무시
        if (PointerUtil.IsOverUI(pointerScreenPos)) return;
        if (inventoryUI != null && inventoryUI.IsHoldingItem) return;
        holdAttack = true;
        // 지금 공격할 수 없으면(회피·넉백·공격 중 등) 선입력으로 기억해 두고 Update에서 실행
        if (!CanStartAttack())
        {
            inputBuffer.Push(BufferedAction.Attack);
            return;
        }

        StartAttack();
    }

    // 방향은 실행 시점의 마우스 위치 기준 (선입력도 실행될 때 다시 조준)
    private void StartAttack()
    {
        Vector3 attackDir = ComputeAttackDirection();
        attackPivot.rotation = Quaternion.LookRotation(attackDir);
        playerFacing.UpdateFacing(attackDir, cameraRight, cameraForwardFlat);

        // 핫바에서 고른 아이템이 도구면 그 도구로, 아니면 맨손으로 공격 (손에 든 외형은 PlayerHeldItemVisual이 담당)
        ToolItemData tool = hotbar.SelectedStack.IsEmpty ? null : hotbar.SelectedStack.item as ToolItemData;
        CurrentElement = tool != null ? tool.element : ElementType.None;
        if (tool != null)
        {
            hitboxController.SetWeapon(tool.damage, tool.toolType, tool.tier, CurrentElement, tool.strike);
        }
        else
        {
            hitboxController.SetWeapon(bareHandDamage, ToolType.None, 0, CurrentElement, StrikeType.None);
        }
        ApplyRange(tool != null ? tool.rangeScale : Vector2.one);

        attackPhase = AttackPhase.Windup;
        phaseTime = 0f;
        lastAttackTime = Time.time;

        playerState.SetState(PlayerActionState.Attack);
        // 공격 모션은 이동 입력이 들어오면 걷기로 넘어가므로, 직전 이동 상태가 남아 바로 끊기지 않도록 정리
        animator.SetBool("IsMoving", false);
        animator.SetBool("IsSprinting", false);
        animator.SetTrigger("Attack");
    }

    // 히트박스를 도구의 범위 배율만큼 키움 (x: 좌우, y: 앞쪽). 몸 쪽 가장자리는 그대로 두고 앞으로 늘림
    private void ApplyRange(Vector2 scale)
    {
        if (hitboxBox == null) return;

        float nearEdge = hitboxBaseCenter.z - hitboxBaseSize.z * 0.5f;
        var size = new Vector3(hitboxBaseSize.x * scale.x, hitboxBaseSize.y, hitboxBaseSize.z * scale.y);
        hitboxBox.size = size;
        hitboxBox.center = new Vector3(hitboxBaseCenter.x, hitboxBaseCenter.y, nearEdge + size.z * 0.5f);
    }

    private Vector3 ComputeAttackDirection()
    {
        if (PointerUtil.TryGetGroundPoint(pointerScreenPos, transform.position.y, out Vector3 hitPoint))
        {
            Vector3 dir = hitPoint - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
        }

        return cameraForwardFlat; // 예외적으로 교차하지 않을 때의 대비값
    }
}
