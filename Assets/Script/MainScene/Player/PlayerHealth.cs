using UnityEngine;
using UnityEngine.InputSystem;

// 사망 시 시체로 멈춘 뒤 일정 시간이 지나면 시작 위치에서 부활 (월드는 유지)
// 붉은색 유지는 HitFlash, 공격 취소는 PlayerAttack, 허기 정지는 PlayerHunger가 Died 이벤트·IsDead로 각자 처리
public class PlayerHealth : Health
{
    [Tooltip("부활 시간·사망 시 아이템 드랍 여부 (난이도별로 다른 에셋 사용)")]
    [SerializeField] private GameSetting gameSetting;
    [SerializeField] private PlayerState playerState;
    [SerializeField] private Animator animator;
    [SerializeField] private Inventory inventory;
    [Tooltip("사망 시 열려 있으면 닫음")]
    [SerializeField] private InventoryUI inventoryUI;

    private DeathRule Rule => gameSetting.death;

    private Rigidbody rb;
    private Knockback knockback;
    private PlayerInput playerInput;
    private PlayerHunger hunger;

    // 부활 위치 (게임 시작 위치)
    private Vector3 spawnPoint;
    private float respawnTimer;

    // 부활까지 남은 시간 (UI 표시용)
    public float RespawnRemaining => IsDead ? Mathf.Max(0f, respawnTimer) : 0f;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
        knockback = GetComponent<Knockback>();
        playerInput = GetComponent<PlayerInput>();
        hunger = GetComponent<PlayerHunger>();
    }

    private void Start()
    {
        spawnPoint = rb.position;
    }

    private void OnEnable()
    {
        if (knockback != null) knockback.Ended += OnKnockbackEnded;
    }

    private void OnDisable()
    {
        if (knockback != null) knockback.Ended -= OnKnockbackEnded;
    }

    private void Update()
    {
        if (!IsDead) return;

        respawnTimer -= Time.deltaTime;
        if (respawnTimer <= 0f) Respawn();
    }

    protected override void Die()
    {
        base.Die();

        playerState.SetState(PlayerActionState.Dead);
        // 이동·공격·핫바·인벤토리 등 모든 조작 차단
        if (playerInput != null) playerInput.DeactivateInput();
        if (inventoryUI != null) inventoryUI.SetOpen(false);

        StopHorizontal();
        animator.SetBool("IsMoving", false);
        animator.SetBool("IsSprinting", false);
        // 몬스터와 같이 마지막 포즈로 정지
        animator.speed = 0f;

        if (Rule.dropItemsOnDeath) DropAllItems();

        respawnTimer = Rule.respawnDelay;
    }

    private void Respawn()
    {
        rb.position = spawnPoint;
        transform.position = spawnPoint;
        rb.linearVelocity = Vector3.zero;

        // Revived → HitFlash 색 복구, RespawnUI 숨김
        Revive();
        if (hunger != null) hunger.Refill();

        animator.speed = 1f;
        playerState.Revive();
        if (playerInput != null) playerInput.ActivateInput();
    }

    // 넉백은 종료 시 속도를 이동 로직에 넘기는데, 사망 중에는 이동 로직이 멈춰 있으므로 미끄러지지 않도록 수평 속도 제거
    private void OnKnockbackEnded()
    {
        if (IsDead) StopHorizontal();
    }

    private void StopHorizontal()
    {
        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(0f, velocity.y, 0f);
    }

    private void DropAllItems()
    {
        if (inventory == null) return;

        if (ItemDropper.Instance == null)
        {
            Debug.LogWarning("[PlayerHealth] 씬에 ItemDropper가 없어 사망 시 아이템을 떨어뜨리지 못함", this);
            return;
        }

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            ItemStack slot = inventory.Get(i);
            if (slot.IsEmpty) continue;

            ItemDropper.Instance.Drop(slot.item, slot.count, transform.position);
            inventory.Remove(i, slot.count);
        }
    }
}
