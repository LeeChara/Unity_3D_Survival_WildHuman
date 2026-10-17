using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

// 플레이어 장비 칸 (0 머리, 1 몸, 2~4 유물). 칸 이동은 인벤토리와 같은 SlotTransfer를 씀
// 받는 피해 계산(방어력 배율·속성 저항·특수효과)을 PlayerHealth에, 공격 속성·주는 넉백을 PlayerAttack에 제공
// 받는 넉백 감소는 같은 오브젝트의 Knockback에 직접 연결
public class PlayerEquipment : MonoBehaviour, ISlotContainer, ISaveable
{
    public const int HeadIndex = 0;
    public const int BodyIndex = 1;
    public const int RelicStart = 2;
    public const int RelicCount = 3;

    private readonly ItemStack[] slots = { new(), new(), new(), new(), new() };
    // 칸별로 특수효과의 OnEquipped를 호출한 장비 (바뀌었을 때 OnUnequipped를 부를 대상)
    private readonly EquipmentItemData[] equipped = new EquipmentItemData[RelicStart + RelicCount];

    private Health health;

    public int SlotCount => slots.Length;

    // 장착한 모든 장비의 방어력 합
    public int Defense { get; private set; }
    // 공격에 실릴 속성 (앞쪽 칸의 장비 우선, 들고 있는 도구에 속성이 있으면 PlayerAttack이 도구 쪽을 씀)
    public ElementType AttackElement { get; private set; }
    // 받는 넉백 감소 비율 (곱으로 합침)
    public float KnockbackResistance { get; private set; }
    // 주는 넉백 배율 (증가 비율을 더한 뒤 1을 더함)
    public float KnockbackScale { get; private set; } = 1f;

    public event Action<int> SlotChanged;

    // 허기 감소 속도 배율 (HungerRateEffect를 곱으로 합침)
    public float HungerRate { get; private set; } = 1f;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (TryGetComponent(out Knockback knockback)) knockback.ResistanceProvider = () => KnockbackResistance;
        if (TryGetComponent(out StatusEffectHost host)) host.IsImmune = IsImmuneTo;
    }

    // 다른 컴포넌트가 Awake에서 정한 빙판 예외(구르기 등)를 유지한 채 장비 조건을 덧붙임
    private void Start()
    {
        if (!TryGetComponent(out IceSlider iceSlider)) return;
        System.Func<bool> baseBypass = iceSlider.Bypass;
        iceSlider.Bypass = () => (baseBypass != null && baseBypass()) || HasEffect<IceGripEffect>();
    }

    public bool HasEffect<T>() where T : EquipmentEffect
    {
        foreach (EquipmentItemData item in equipped)
        {
            if (item == null || item.effects == null) continue;
            foreach (EquipmentEffect effect in item.effects)
            {
                if (effect is T) return true;
            }
        }
        return false;
    }

    private bool IsImmuneTo(StatusEffectData data)
    {
        foreach (EquipmentItemData item in equipped)
        {
            if (item == null || item.effects == null) continue;
            foreach (EquipmentEffect effect in item.effects)
            {
                if (effect is StatusImmunityEffect immunity && immunity.Blocks(data)) return true;
            }
        }
        return false;
    }

    private void OnEnable()
    {
        if (health != null) health.Hit += OnHit;
    }

    private void OnDisable()
    {
        if (health != null) health.Hit -= OnHit;
    }

    public ItemStack Get(int index) => slots[index];

    // 칸 번호의 칸 종류
    public static EquipmentSlot SlotTypeOf(int index) => index switch
    {
        HeadIndex => EquipmentSlot.Head,
        BodyIndex => EquipmentSlot.Body,
        _ => EquipmentSlot.Relic,
    };

    // 칸 종류가 맞는 장비만 놓을 수 있음 (랜턴은 유물 칸에 넣으면 손에 들지 않아도 빛을 냄)
    public bool CanPlace(int index, ItemData item)
    {
        if (item is LanternItemData) return SlotTypeOf(index) == EquipmentSlot.Relic;
        return item is EquipmentItemData equipment && equipment.slot == SlotTypeOf(index);
    }

    // 유물 칸에 낀 첫 랜턴 (없으면 null)
    public LanternItemData EquippedLantern
    {
        get
        {
            for (int i = RelicStart; i < RelicStart + RelicCount; i++)
            {
                if (!slots[i].IsEmpty && slots[i].item is LanternItemData lantern) return lantern;
            }
            return null;
        }
    }

    // 이 장비를 바로 장착할 칸 (유물은 앞쪽 빈칸, 모두 차 있으면 첫 유물 칸과 맞바꿈)
    public int SlotIndexFor(EquipmentItemData item)
    {
        if (item.slot == EquipmentSlot.Head) return HeadIndex;
        if (item.slot == EquipmentSlot.Body) return BodyIndex;

        for (int i = RelicStart; i < RelicStart + RelicCount; i++)
        {
            if (slots[i].IsEmpty) return i;
        }
        return RelicStart;
    }

    public void NotifyChanged(int index)
    {
        RefreshEquipped(index);
        SlotChanged?.Invoke(index);
    }

    // 칸의 장비가 바뀌었으면 이전 장비의 특수효과를 끄고 새 장비의 특수효과를 켬
    private void RefreshEquipped(int index)
    {
        var current = slots[index].IsEmpty ? null : slots[index].item as EquipmentItemData;
        EquipmentItemData previous = equipped[index];
        if (current == previous) return;

        if (previous != null) ForEachEffect(previous, effect => effect.OnUnequipped(this));
        equipped[index] = current;
        if (current != null) ForEachEffect(current, effect => effect.OnEquipped(this));

        RecalculateStats();
    }

    private void RecalculateStats()
    {
        int defense = 0;
        ElementType element = ElementType.None;
        float keep = 1f;
        float bonus = 0f;
        float hunger = 1f;

        foreach (EquipmentItemData item in equipped)
        {
            if (item == null) continue;
            defense += item.defense;
            if (element == ElementType.None) element = item.attackElement;
            keep *= 1f - item.knockbackResistance;
            bonus += item.knockbackBonus;
            if (item.effects == null) continue;
            foreach (EquipmentEffect effect in item.effects)
            {
                if (effect is HungerRateEffect rate) hunger *= rate.multiplier;
            }
        }
        HungerRate = hunger;

        Defense = defense;
        AttackElement = element;
        KnockbackResistance = 1f - keep;
        KnockbackScale = 1f + bonus;
    }

    #region 피해 계산

    // 공격 피해: 방어력 배율(환경 피해 제외) × 속성 저항 → 최소 1 → 특수효과 보정
    public int CalculateHitDamage(AttackData attack)
    {
        if (attack.damage <= 0) return attack.damage;

        float damage = attack.damage;
        if (!attack.environmental) damage *= EquipmentDefense.Multiplier(Defense, attack.damage);

        int result = ApplyResist(damage, attack.element);
        foreach (EquipmentItemData item in equipped)
        {
            if (item == null || item.effects == null) continue;
            foreach (EquipmentEffect effect in item.effects)
            {
                if (effect != null) result = effect.ModifyIncomingDamage(this, attack, result);
            }
        }
        return Mathf.Max(0, result);
    }

    // 상태이상 지속 피해: 속성 저항만 적용
    public int CalculateStatusDamage(int damage, ElementType element)
    {
        if (damage <= 0) return damage;
        return ApplyResist(damage, element);
    }

    // 완전히 막으면 0, 아니면 최소 1
    private int ApplyResist(float damage, ElementType element)
    {
        float multiplier = ResistMultiplier(element);
        if (multiplier <= 0f) return 0;
        return Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
    }

    // 장착한 모든 장비의 저항을 곱으로 합침 (속성 없는 공격은 1)
    public float ResistMultiplier(ElementType element)
    {
        if (element == ElementType.None) return 1f;

        float multiplier = 1f;
        foreach (EquipmentItemData item in equipped)
        {
            if (item == null || item.resists == null) continue;
            foreach (ElementResist resist in item.resists)
            {
                if (resist.element == element) multiplier *= 1f - resist.reduction;
            }
        }
        return multiplier;
    }

    private void OnHit(AttackData attack)
    {
        foreach (EquipmentItemData item in equipped)
        {
            if (item != null) ForEachEffect(item, effect => effect.OnHit(this, attack));
        }
    }

    private static void ForEachEffect(EquipmentItemData item, Action<EquipmentEffect> action)
    {
        if (item.effects == null) return;
        foreach (EquipmentEffect effect in item.effects)
        {
            if (effect != null) action(effect);
        }
    }

    #endregion

    #region 저장

    public string SaveKey => "equipment";

    // 칸 순서 그대로 저장 (빈칸은 null)
    public JToken Save()
    {
        var array = new JArray();
        foreach (ItemStack slot in slots)
        {
            array.Add(slot.IsEmpty ? (JToken)JValue.CreateNull() : slot.item.id);
        }
        return new JObject { ["slots"] = array };
    }

    // 지금은 없거나 칸 종류가 바뀐 장비는 비워 둠
    public void Load(JToken data)
    {
        var array = data["slots"] as JArray;

        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack slot = slots[i];
            slot.Clear();

            string id = array != null && i < array.Count && array[i].Type == JTokenType.String ? array[i].Value<string>() : null;
            ItemData item = id != null ? SaveRegistry.Active.GetItem(id) : null;
            if (item != null && CanPlace(i, item))
            {
                slot.item = item;
                slot.count = 1;
            }
            NotifyChanged(i);
        }
    }

    #endregion
}
