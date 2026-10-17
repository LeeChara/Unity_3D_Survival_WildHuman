using UnityEngine;

// 장비의 특수효과 (종류마다 이 클래스를 상속한 에셋을 만들어 장비의 effects에 넣음)
// 에셋 하나를 여러 장비가 함께 쓸 수 있으므로 장착별 상태는 에셋에 두지 않고 owner 쪽에 둠
public abstract class EquipmentEffect : ScriptableObject
{
    [Tooltip("툴팁의 장비 스탯 아래에 한 줄로 표시 (비워두면 표시하지 않음)")]
    [TextArea] public string description;

    // 장비를 칸에 넣었을 때 / 뺐을 때
    public virtual void OnEquipped(PlayerEquipment owner) { }
    public virtual void OnUnequipped(PlayerEquipment owner) { }

    // 방어력 배율·속성 저항을 적용한 뒤의 피해를 한 번 더 보정 (예: 강타에 맞으면 피해 증가)
    public virtual int ModifyIncomingDamage(PlayerEquipment owner, AttackData attack, int damage) => damage;

    // 공격을 받은 직후 (피해 적용 후, 피해가 0이어도 호출. 예: 맞으면 음파 발생)
    public virtual void OnHit(PlayerEquipment owner, AttackData attack) { }
}
