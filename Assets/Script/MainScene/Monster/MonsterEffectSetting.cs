using UnityEngine;

// 모든 몬스터가 공유하는 상태 이펙트 설정
// 몬스터별로 달라야 하는 값(lowHealthRatio 등)은 MonsterData에 둠
// 피격 반짝임 설정은 플레이어와 공용이므로 HitFlashSetting에서 관리
[CreateAssetMenu(fileName = "MonsterEffectSetting", menuName = "Monster/MonsterEffectSetting")]
public class MonsterEffectSetting : ScriptableObject
{
    [Header("말풍선")]
    public float emoteDuration = 1f;

    [Header("사망")]
    [Tooltip("사망 후 시체가 남아 있는 시간")]
    public float corpseDuration = 1f;
    [Tooltip("시체가 사라질 때 생성할 이펙트 (비워두면 생성하지 않음)")]
    public GameObject deathEffectPrefab;
}
