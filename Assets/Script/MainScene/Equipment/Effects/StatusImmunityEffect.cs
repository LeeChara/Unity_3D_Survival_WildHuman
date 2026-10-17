using UnityEngine;

// 지정한 상태이상에 걸리지 않음 (장착하는 순간 이미 걸려 있던 것도 풀림, 예: 젖음 면역)
[CreateAssetMenu(fileName = "StatusImmunityEffect", menuName = "Item/Equipment Effect/Status Immunity")]
public class StatusImmunityEffect : EquipmentEffect
{
    public StatusEffectData[] immuneTo;

    public bool Blocks(StatusEffectData data)
    {
        if (immuneTo == null) return false;
        foreach (StatusEffectData immune in immuneTo)
        {
            if (immune == data) return true;
        }
        return false;
    }

    public override void OnEquipped(PlayerEquipment owner)
    {
        if (immuneTo == null || !owner.TryGetComponent(out StatusEffectHost host)) return;
        foreach (StatusEffectData immune in immuneTo)
        {
            if (immune != null) host.Remove(immune);
        }
    }
}
