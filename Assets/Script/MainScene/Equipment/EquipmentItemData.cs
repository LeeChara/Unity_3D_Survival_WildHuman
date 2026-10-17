using System;
using System.Text;
using UnityEngine;

// 장비 칸 종류 (PlayerEquipment의 칸 번호와 같은 순서)
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
// 머리·몸은 방어력으로 받는 피해를 줄이고(EquipmentDefense), 유물은 방어 없이 특수효과만 가짐
[CreateAssetMenu(fileName = "EquipmentItemData", menuName = "Item/EquipmentItemData")]
public class EquipmentItemData : ItemData
{
    public EquipmentSlot slot;

    [Tooltip("방어력 (머리·몸의 합을 공격 피해와 비교해 받는 피해 배율이 정해짐, 같으면 20%를 받음. 유물은 사용하지 않음)")]
    [Min(0)] public int defense;

    [Tooltip("속성 피해 저항 (몬스터 공격·환경 피해·상태이상 지속 피해에 모두 적용)")]
    public ElementResist[] resists;

    [Tooltip("특수효과")]
    public EquipmentEffect[] effects;

    public bool HasArmor => slot != EquipmentSlot.Relic;

    public override string GetStatText()
    {
        var sb = new StringBuilder(SlotName(slot));
        if (HasArmor) sb.Append($"\n방어 {defense}");

        if (resists != null)
        {
            foreach (ElementResist resist in resists)
            {
                string element = ToolItemData.ElementName(resist.element);
                if (element.Length == 0 || resist.reduction <= 0f) continue;
                sb.Append($"\n{element} 저항 {Mathf.RoundToInt(resist.reduction * 100f)}%");
            }
        }

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
