using UnityEngine;

// 젖음 공용 규칙 (지속 시간, 표시)
// 젖음 = 체력이 있는 물체 + 물 속성
// - 젖음 자체에는 효과가 없음 (표시만)
// - 타는 물체가 젖으면 불이 꺼짐
// - 젖은 물체에 불이 닿으면 불은 붙지 않고 젖음만 마름
// - 이후 감전(젖은 물체는 금속처럼 전기가 통함)·빙결(젖은 물체만 얼 수 있음)의 조건이 됨
[CreateAssetMenu(fileName = "WaterSetting", menuName = "Common/WaterSetting")]
public class WaterSetting : ScriptableObject
{
    [Header("상태")]
    [Tooltip("젖음 상태 (피해 없음, 플레이어 상태 아이콘 표시용)")]
    public StatusEffectData wet;
    [Tooltip("생물(플레이어·몬스터)이 젖어 있는 시간 (초, 물에서 나온 뒤부터)")]
    [Min(0.1f)] public float creatureWetDuration = 10f;
    [Tooltip("그 밖의 물체(자원·설치물)가 젖어 있는 시간 (초)")]
    [Min(0.1f)] public float objectWetDuration = 60f;

    [Header("물 지형")]
    [Tooltip("물 칸 위에서의 평소 이동 속도 배율 (구르기·공격 돌진·넉백은 제외)")]
    [Range(0.1f, 1f)] public float waterSpeedMultiplier = 0.7f;
    [Tooltip("물 호수 트리거 레이어 (발밑이 물 칸인지 확인)")]
    public LayerMask waterLayers;

    [Header("빙판")]
    [Tooltip("빙판 호수 트리거 레이어 (발밑이 빙판 칸인지 확인)")]
    public LayerMask iceLayers;
    [Tooltip("빙판 위에서 속도가 바뀌는 빠르기 (m/s², 작을수록 오래 미끄러짐)")]
    [Min(0.1f)] public float iceAcceleration = 4f;

    [Header("표시")]
    [Tooltip("젖은 동안 그림에 곱하는 색 (조금 어둡게)")]
    public Color wetTint = new Color(0.72f, 0.76f, 0.85f);
    [Tooltip("젖은 동안 떨어지는 물방울")]
    public Sprite dropSprite;
    [Tooltip("젖은 물체에 불이 닿아 마를 때 피어오르는 김 파티클")]
    public EffectData steamEffect;
    [Tooltip("물을 끼얹었을 때 튀는 물방울 파티클")]
    public EffectData splashEffect;
}
