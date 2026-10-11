using UnityEngine;

// 지속 시간 동안 일정 간격으로 피해를 주는 상태 이상 (예: 화상)
// 속성 공격에 맞으면 걸리도록 appliedBy로 속성과 연결 (StatusEffectHost가 판단)
[CreateAssetMenu(fileName = "StatusEffectData", menuName = "Status/StatusEffectData")]
public class StatusEffectData : ScriptableObject
{
    [Tooltip("저장·식별용 고유 ID (한 번 정하면 변경하지 않음)")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Tooltip("이 속성 공격에 맞으면 걸림 (None이면 속성 공격으로는 걸리지 않음)")]
    public ElementType appliedBy;

    [Header("지속 피해")]
    [Tooltip("지속 시간 (초, 다시 걸리면 처음부터)")]
    [Min(0.1f)] public float duration = 4f;
    [Tooltip("피해 간격 (초)")]
    [Min(0.1f)] public float tickInterval = 1f;
    [Min(0)] public int tickDamage = 2;

    [Header("표시")]
    [Tooltip("걸려 있는 동안 대상 위에 반복 재생할 프레임")]
    public Sprite[] indicatorFrames;
    [Min(0.1f)] public float frameRate = 8f;
}
