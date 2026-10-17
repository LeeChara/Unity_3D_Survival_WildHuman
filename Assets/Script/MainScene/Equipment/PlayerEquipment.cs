using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

// 플레이어 장비 칸 (0 머리, 1 몸, 2 유물). 칸 이동은 인벤토리와 같은 SlotTransfer를 씀
// 받는 피해 계산(방어력 배율·속성 저항·특수효과)을 PlayerHealth에 제공
public class PlayerEquipment : MonoBehaviour, ISlotContainer, ISaveable
{
    private readonly ItemStack[] slots = { new(), new(), new() };
    // 칸별로 특수효과의 OnEquipped를 호출한 장비 (바뀌었을 때 OnUnequipped를 부를 대상)
    private readonly EquipmentItemData[] equipped = new EquipmentItemData[3];

    private Health health;

    public int SlotCount => slots.Length;

    // 머리·몸 방어력의 합
    public int Defense { get; private set; }

    public event Action<int> SlotChanged;

    private void Awake()
    {
        health = GetComponent<Health>();
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

    public EquipmentItemData GetEquipped(EquipmentSlot slot) => slots[(int)slot].item as EquipmentItemData;

    // 칸 종류가 맞는 장비만 놓을 수 있음
    public bool CanPlace(int index, ItemData item) => item is EquipmentItemData equipment && (int)equipment.slot == index;

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

        Defense = DefenseOf(EquipmentSlot.Head) + DefenseOf(EquipmentSlot.Body);
    }

    private int DefenseOf(EquipmentSlot slot)
    {
        EquipmentItemData item = equipped[(int)slot];
        return item != null ? item.defense : 0;
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
