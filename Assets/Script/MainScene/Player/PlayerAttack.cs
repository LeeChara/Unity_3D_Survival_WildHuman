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

    public float windupDuration = 0.2f;
    public float attackDuration = 0.5f;
    public float recoverDuration = 0.1f;
    public float attackCooldown = 1.5f;

    [SerializeField] private float cameraYAngle = 45f; // PlayerMovement/Billboard의 값과 반드시 일치해야함
    private Vector3 cameraRight;
    private Vector3 cameraForwardFlat;

    private Vector2 pointerScreenPos;

    private enum AttackPhase { None, Windup, Attack, Recover }
    private AttackPhase attackPhase = AttackPhase.None;
    private float phaseTime;
    private float lastAttackTime = -999f; // 시작 시 바로 사용 가능하도록 충분히 작은 값
    private void Awake()
    {
        hitboxObject.SetActive(false);

        Quaternion cameraRotation = Quaternion.Euler(0, cameraYAngle, 0);
        cameraRight = cameraRotation * Vector3.right;
        cameraForwardFlat = cameraRotation * Vector3.forward;

        // 공격 도중 넉백되면 Update가 멈추므로 히트박스가 켜진 채 남지 않도록 공격을 취소
        if (TryGetComponent<Knockback>(out Knockback knockback))
        {
            knockback.Started += CancelAttack;
        }
    }

    private void CancelAttack()
    {
        if (attackPhase == AttackPhase.None) return;

        attackPhase = AttackPhase.None;
        hitboxObject.SetActive(false);
        animator.ResetTrigger("Attack");
    }

    private void Update()
    {
        // Attack일 때만 실행 (다른 클래스와 배타적)
        if (playerState.CurrentState != PlayerActionState.Attack) return;

        phaseTime += Time.deltaTime;

        switch (attackPhase)
        {
            case AttackPhase.Windup:
                if (phaseTime >= windupDuration)
                {
                    attackPhase = AttackPhase.Attack;
                    phaseTime = 0f;

                    hitboxController.ResetHitTargets();
                    hitboxObject.SetActive(true);
                }
                break;

            case AttackPhase.Attack:
                if (phaseTime >= attackDuration)
                {
                    attackPhase = AttackPhase.Recover;
                    phaseTime = 0f;

                    hitboxObject.SetActive(false);
                }    
                break;

            case AttackPhase.Recover:
                if (phaseTime >= recoverDuration)
                {
                    attackPhase = AttackPhase.None;
                    playerState.SetState(PlayerActionState.Normal);
                }
                break;
        }
    }

    void OnPoint(InputValue value)
    {
        pointerScreenPos = value.Get<Vector2>();
    }

    void OnAttack(InputValue value)
    {
        // UI(인벤토리 칸 등) 위를 클릭한 경우 무시
        if (PointerUtil.IsOverUI(pointerScreenPos)) return;
        // 회피 또는 공격 중에는 입력 무시
        if (!playerState.CanAct) return;
        // 쿨다운 중에는 입력 무시
        if (Time.time < lastAttackTime + attackCooldown) return;

        Vector3 attackDir = ComputeAttackDirection();
        attackPivot.rotation = Quaternion.LookRotation(attackDir);
        playerFacing.UpdateFacing(attackDir, cameraRight, cameraForwardFlat);

        // 핫바에서 고른 아이템이 도구면 그 도구로 공격 (장착 외형은 추후 추가)
        ToolItemData tool = hotbar.SelectedStack.IsEmpty ? null : hotbar.SelectedStack.item as ToolItemData;
        hitboxController.SetToolType(tool != null ? tool.toolType : ToolType.None);

        attackPhase = AttackPhase.Windup;
        phaseTime = 0f;
        lastAttackTime = Time.time;

        playerState.SetState(PlayerActionState.Attack);
        animator.SetTrigger("Attack");
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
