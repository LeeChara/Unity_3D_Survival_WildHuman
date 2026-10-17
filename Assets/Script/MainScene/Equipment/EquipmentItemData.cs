using System;
using System.Text;
using UnityEngine;

// 장비 칸 종류 (칸 번호와의 대응은 PlayerEquipment.SlotTypeOf)
public enum EquipmentSlot
{
    Head,
    Body,
    Relic
}

// 속성 하나에 대한 저항 (여러 장비의 저항은 곱으로 합쳐짐: 50% + 50% → 75%)
[Serializable]
public struct ElementResist
{
    public ElementType element;
    [Tooltip("이 속성 피해를 줄이는 비율 (1 = 완전히 막음)")]
    [Range(0f, 1f)] public float reduction;
}

// 플레이어가 머리·몸·유물 칸에 장착하는 아이템
// 장착한 모든 장비의 방어력 합으로 받는 피해를 줄이고(EquipmentDefense), 부가 능력·특수효과를 더함
[CreateAssetMenu(fileName = "EquipmentItemData", menuName = "Item/EquipmentItemData")]
public class EquipmentItemData : ItemData
{
    public EquipmentSlot slot;

    [Tooltip("방어력 (장착한 모든 장비의 합을 공격 피해와 비교해 받는 피해 배율이 정해짐, 피해의 60%까지는 그만큼 빼고 그 뒤로는 점점 덜 줄어듦)")]
    [Min(0)] public int defense;

    [Tooltip("속성 피해 저항 (몬스터 공격·환경 피해·상태이상 지속 피해에 모두 적용)")]
    public ElementResist[] resists;

    [Header("부가 능력")]
    [Tooltip("공격 속성 (들고 있는 도구에 속성이 없을 때 공격에 실림)")]
    public ElementType attackElement;
    [Tooltip("받는 넉백 거리 감소 비율 (여러 장비는 곱으로 합쳐짐)")]
    [Range(0f, 1f)] public float knockbackResistance;
    [Tooltip("주는 넉백 거리 증가 비율 (여러 장비는 더해짐, 0.5 = 50% 증가)")]
    [Min(0f)] public float knockbackBonus;

    [Tooltip("특수효과")]
    public EquipmentEffect[] effects;

    public override string GetStatText()
    {
        var sb = new StringBuilder(SlotName(slot));
        if (defense > 0 || slot != EquipmentSlot.Relic) sb.Append($"\n방어 {defense}");

        if (resists != null)
        {
            foreach (ElementResist resist in resists)
            {
                string element = ToolItemData.ElementName(resist.element);
                if (element.Length == 0 || resist.reduction <= 0f) continue;
                sb.Append($"\n{element} 저항 {Mathf.RoundToInt(resist.reduction * 100f)}%");
            }
        }

        string attack = ToolItemData.ElementName(attackElement);
        if (attack.Length > 0) sb.Append($"\n공격에 {attack} 속성");
        if (knockbackResistance > 0f) sb.Append($"\n받는 넉백 {Mathf.RoundToInt(knockbackResistance * 100f)}% 감소");
        if (knockbackBonus > 0f) sb.Append($"\n주는 넉백 {Mathf.RoundToInt(knockbackBonus * 100f)}% 증가");

        if (effects != null)
        {
            foreach (EquipmentEffect effect in effects)
            {
                if (effect != null && !string.IsNullOrEmpty(effect.description)) sb.Append($"\n{effect.description}");
            }
        }
        return sb.ToString();
    }

    public static string SlotName(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Head => "머리 방어구",
        EquipmentSlot.Body => "몸 방어구",
        _ => "유물",
    };

    // 장비는 한 칸에 하나만
    private void OnValidate()
    {
        maxStack = 1;
    }
}
