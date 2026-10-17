using UnityEngine;

// 허기가 줄어드는 속도를 바꿈 (여러 개는 곱으로 합쳐짐, PlayerHunger가 PlayerEquipment.HungerRate로 읽음)
[CreateAssetMenu(fileName = "HungerRateEffect", menuName = "Item/Equipment Effect/Hunger Rate")]
public class HungerRateEffect : EquipmentEffect
{
    [Tooltip("허기 감소 속도 배율 (0.75 = 25% 느리게)")]
    [Min(0f)] public float multiplier = 0.75f;
}
