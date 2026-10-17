using Newtonsoft.Json.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

// 사망 시 시체로 멈춘 뒤 일정 시간이 지나면 시작 위치에서 부활 (월드는 유지)
// 붉은색 유지는 HitFlash, 공격 취소는 PlayerAttack, 허기 정지는 PlayerHunger가 Died 이벤트·IsDead로 각자 처리
// 시체가 밀려 미끄러지지 않도록 멈추는 처리는 PlayerMovement가 Dead 상태로 담당
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
    private ItemCollector itemCollector;
    private PlayerInput playerInput;
    private PlayerHunger hunger;
    private PlayerEquipment equipment;

    // 부활 위치 (월드 시작 지점)
    private Vector3 spawnPoint;
    private float respawnTimer;

    public Vector3 SpawnPoint => spawnPoint;

    // 부활까지 남은 시간 (UI 표시용)
    public float RespawnRemaining => IsDead ? Mathf.Max(0f, respawnTimer) : 0f;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
        itemCollector = GetComponent<ItemCollector>();
        playerInput = GetComponent<PlayerInput>();
        hunger = GetComponent<PlayerHunger>();
        equipment = GetComponent<PlayerEquipment>();
    }

    // 장비의 방어력 배율·속성 저항·특수효과를 적용 (환경 피해는 속성 저항만)
    protected override int CalculateDamage(AttackData attack)
    {
        return equipment != null ? equipment.CalculateHitDamage(attack) : base.CalculateDamage(attack);
    }

    // 지속 피해에는 장비의 속성 저항만 적용
    protected override int CalculateStatusDamage(int damage, ElementType element)
    {
        return equipment != null ? equipment.CalculateStatusDamage(damage, element) : damage;
    }

    // 월드 생성 시 정해진 시작 지점에서 시작 (높이는 씬에 배치된 값 유지)
    private void Start()
    {
        spawnPoint = rb.position;
        if (WorldState.Instance == null) return;

        Vector3 start = WorldState.Instance.SpawnPoint;
        spawnPoint = new Vector3(start.x, spawnPoint.y, start.z);
        rb.position = spawnPoint;
        transform.position = spawnPoint;
    }

    // 저장된 위치로 순간 이동 (카메라가 따라 미끄러지지 않도록 Cinemachine에 알림)
    public void WarpTo(Vector3 position)
    {
        Vector3 delta = position - transform.position;
        rb.position = position;
        transform.position = position;
        rb.linearVelocity = Vector3.zero;
        CinemachineCore.OnTargetObjectWarped(transform, delta);
    }

    // 사망 중이면 부활할 때의 체력(가득)으로 저장
    public override JToken Save()
    {
        if (IsDead) return new JObject { ["hp"] = MaxHealth };
        return base.Save();
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
        // 시체가 아이템을 줍지 않도록 수집기를 끔 (끌어가던 아이템도 놓아줌)
        if (itemCollector != null) itemCollector.enabled = false;

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
        if (itemCollector != null) itemCollector.enabled = true;
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

        if (equipment == null) return;
        for (int i = 0; i < equipment.SlotCount; i++)
        {
            ItemStack slot = equipment.Get(i);
            if (slot.IsEmpty) continue;

            ItemDropper.Instance.Drop(slot.item, slot.count, transform.position);
            SlotTransfer.Remove(equipment, i, slot.count);
        }
    }
}
