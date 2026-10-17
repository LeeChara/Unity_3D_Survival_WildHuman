using UnityEngine;

// 플레이어 효과음: 발소리, 휘두르기·구르기, 피격·사망·부활·기절, 먹기·설치·줍기·제작, 장비 장착·해제, 핫바 칸 바꾸기, 허기 경고, 체력이 낮을 때 심장 소리
// 각 기능의 이벤트를 구독해서 소리만 냄 (소리를 뺄 때 이 컴포넌트만 지우면 됨)
// 소리 목록은 SfxSetting, 바이옴별 발소리는 BiomeData.footstepCue
public class PlayerSfx : MonoBehaviour
{
    private Health health;
    private PlayerState playerState;
    private PlayerAttack attack;
    private PlayerDodge dodge;
    private PlayerHotbar hotbar;
    private PlayerHunger hunger;
    private PlayerItemUser itemUser;
    private PlayerCrafter crafter;
    private ItemCollector collector;
    private WaterWader waterWader;
    private IceSlider iceSlider;
    private PlayerEquipment equipment;
    // 칸별로 마지막에 본 장비 (장착·해제·교체를 구분)
    private ItemData[] equippedItems;

    private Vector3 lastPosition;
    private float travelled;
    private float heartbeatTimer;

    private static SfxSetting S => SfxSetting.Instance;

    private void Awake()
    {
        health = GetComponent<Health>();
        playerState = GetComponentInChildren<PlayerState>();
        attack = GetComponentInChildren<PlayerAttack>();
        dodge = GetComponentInChildren<PlayerDodge>();
        hotbar = GetComponentInChildren<PlayerHotbar>();
        hunger = GetComponentInChildren<PlayerHunger>();
        itemUser = GetComponentInChildren<PlayerItemUser>();
        crafter = GetComponentInChildren<PlayerCrafter>();
        collector = GetComponentInChildren<ItemCollector>();
        waterWader = GetComponent<WaterWader>();
        iceSlider = GetComponent<IceSlider>();
        equipment = GetComponentInChildren<PlayerEquipment>();
        if (equipment != null) equippedItems = new ItemData[equipment.SlotCount];
    }

    // 씬을 연 직후에는 세이브를 불러오며 장비·핫바가 바뀌므로 소리를 내지 않음
    private static bool Loading => Time.timeSinceLevelLoad < 1f;

    private void OnEnable()
    {
        lastPosition = transform.position;
        travelled = 0f;

        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            health.Revived += OnRevived;
        }
        if (playerState != null) playerState.StunStarted += OnStunned;
        if (attack != null) attack.AttackStarted += OnAttackStarted;
        if (dodge != null) dodge.Dodged += OnDodged;
        if (hunger != null) hunger.HungerChanged += OnHungerChanged;
        if (itemUser != null) itemUser.ItemUsed += OnItemUsed;
        if (crafter != null) crafter.Crafted += OnCrafted;
        if (collector != null) collector.Collected += OnCollected;
        if (equipment != null) equipment.SlotChanged += OnEquipmentChanged;
        if (hotbar != null) hotbar.SelectionChanged += OnHotbarSelected;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            health.Revived -= OnRevived;
        }
        if (playerState != null) playerState.StunStarted -= OnStunned;
        if (attack != null) attack.AttackStarted -= OnAttackStarted;
        if (dodge != null) dodge.Dodged -= OnDodged;
        if (hunger != null) hunger.HungerChanged -= OnHungerChanged;
        if (itemUser != null) itemUser.ItemUsed -= OnItemUsed;
        if (crafter != null) crafter.Crafted -= OnCrafted;
        if (collector != null) collector.Collected -= OnCollected;
        if (equipment != null) equipment.SlotChanged -= OnEquipmentChanged;
        if (hotbar != null) hotbar.SelectionChanged -= OnHotbarSelected;
    }

    private void Update()
    {
        if (S == null) return;
        UpdateFootsteps();
        UpdateHeartbeat();
    }

    // 걸은 거리마다 발소리 (Footprinter와 같은 방식, 구르기·넉백 중에는 내지 않음)
    private void UpdateFootsteps()
    {
        Vector3 position = transform.position;
        Vector3 delta = position - lastPosition;
        delta.y = 0f;
        lastPosition = position;

        float distance = delta.magnitude;
        // 순간이동(부활, 세이브 복원)은 걸음으로 치지 않음
        if (distance <= 0.0001f || distance > S.footstepStride * 4f) return;
        if (health != null && health.IsDead) return;
        if (playerState != null && playerState.CurrentState != PlayerActionState.Normal) return;

        travelled += distance;
        if (travelled < S.footstepStride) return;
        travelled -= S.footstepStride;

        Sfx.Play(FootstepCue(position), position);
    }

    private SfxCue FootstepCue(Vector3 position)
    {
        if (waterWader != null && waterWader.InWater) return S.footstepWater;
        if (iceSlider != null && iceSlider.OnIce) return S.footstepIce;

        WorldState world = WorldState.Instance;
        BiomeData biome = world != null && world.IsInside(position) ? world.GetBiome(world.WorldToChunk(position)) : null;
        return SfxCue.Or(biome != null ? biome.footstepCue : null, S.footstep);
    }

    private void UpdateHeartbeat()
    {
        if (health == null || health.IsDead || health.MaxHealth <= 0f || health.CurrentHealth / health.MaxHealth > S.heartbeatRatio)
        {
            heartbeatTimer = 0f;
            return;
        }

        heartbeatTimer -= Time.deltaTime;
        if (heartbeatTimer > 0f) return;
        heartbeatTimer = S.heartbeatInterval;
        Sfx.PlayUI(S.heartbeat);
    }

    private void OnDamaged()
    {
        if (S != null && !health.IsDead) Sfx.Play(S.playerHurt, transform.position);
    }

    private void OnDied()
    {
        if (S != null) Sfx.Play(S.playerDeath, transform.position);
    }

    private void OnRevived()
    {
        if (S != null) Sfx.PlayUI(S.respawn);
    }

    private void OnStunned()
    {
        if (S != null) Sfx.Play(S.playerStun, transform.position);
    }

    // 강타 무기(망치·철퇴 등)는 묵직한 소리
    private void OnAttackStarted()
    {
        if (S == null) return;
        ToolItemData tool = hotbar != null && !hotbar.SelectedStack.IsEmpty ? hotbar.SelectedStack.item as ToolItemData : null;
        bool heavy = tool != null && tool.strike == StrikeType.Smash;
        Sfx.Play(heavy ? S.swingHeavy : S.swing, transform.position);
    }

    private void OnDodged()
    {
        if (S != null) Sfx.Play(S.dodge, transform.position);
    }

    // 허기가 경고 비율 아래로 내려갈 때와 바닥날 때 한 번씩
    private float lastHungerRatio = 1f;

    private void OnHungerChanged(float current, float max)
    {
        float ratio = max > 0f ? current / max : 1f;
        if (S != null && health != null && !health.IsDead)
        {
            bool crossedWarning = lastHungerRatio > S.hungerWarningRatio && ratio <= S.hungerWarningRatio;
            bool crossedEmpty = lastHungerRatio > 0f && ratio <= 0f;
            if (crossedWarning || crossedEmpty) Sfx.PlayUI(S.hungerWarning);
        }
        lastHungerRatio = ratio;
    }

    private void OnItemUsed(ItemData item)
    {
        if (S == null) return;
        if (item is FoodItemData) Sfx.Play(S.eat, transform.position);
        else if (item is PlaceableItemData) Sfx.Play(S.place, transform.position);
    }

    private void OnCrafted(Recipe recipe)
    {
        if (S != null) Sfx.PlayUI(S.craft);
    }

    private void OnCollected(ItemData item, int count)
    {
        if (S != null) Sfx.Play(S.pickup, transform.position);
    }

    // 칸에 장비가 생기거나 바뀌면 장착, 비면 해제
    private void OnEquipmentChanged(int index)
    {
        ItemStack slot = equipment.Get(index);
        ItemData current = slot.IsEmpty ? null : slot.item;
        ItemData previous = equippedItems[index];
        equippedItems[index] = current;
        if (S == null || Loading || current == previous) return;

        Sfx.PlayUI(current != null ? S.equipOn : S.equipOff);
    }

    private void OnHotbarSelected(int index)
    {
        if (S != null && !Loading) Sfx.PlayUI(S.hotbarSelect);
    }
}
