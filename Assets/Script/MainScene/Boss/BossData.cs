using UnityEngine;

// 보스 몬스터 데이터 (일반 몬스터 데이터 + 페이즈·패턴)
// 기본 능력치·추격·넉백 저항·드랍은 MonsterData 값을 그대로 씀 (Windup/Attack/Recover 항목은 쓰지 않음)
[CreateAssetMenu(fileName = "BossData", menuName = "Monster/BossData")]
public class BossData : MonsterData
{
    [Header("보스")]
    [Tooltip("체력 비율이 이 값 이하가 되면 2페이즈")]
    [Range(0f, 1f)] public float phase2HealthRatio = 0.3f;
    [Tooltip("패턴이 끝난 뒤 다음 패턴을 고르기까지 쉬는 시간 (초)")]
    [Min(0f)] public float patternGap = 0.5f;
    [Header("둥지")]
    [Tooltip("둥지 중심에서 이 거리 밖으로 나간 대상은 쫓지 않음 (싸움이 끝나고 둥지로 돌아감)")]
    [Min(1f)] public float leashRadius = 30f;
    [Tooltip("싸움이 끝난 뒤 초당 회복하는 체력 비율 (다 회복하면 1페이즈로 돌아감)")]
    [Range(0f, 1f)] public float regenPerSecond = 0.05f;
    [Tooltip("싸우지 않을 때 둥지 중심에서 이만큼 벗어나면 돌아감 (m)")]
    [Min(1f)] public float homeWanderRadius = 8f;

    [Tooltip("사용할 패턴 (조건에 맞는 것 중 가중치로 고름)")]
    public BossPattern[] patterns;
    [Tooltip("첫 조우와 페이즈 전환 때 조건과 상관없이 쓰는 패턴 (포효)")]
    public BossPattern roarPattern;

    [Header("2페이즈 불꽃")]
    [Tooltip("2페이즈에 몸을 덮는 불꽃 (젖어 있는 동안은 꺼지고 공격에 화염 속성도 실리지 않음)")]
    public BurnEffect bodyFlame;
    [Tooltip("불의 고리·화염 장판을 이루는 불길")]
    public GroundFlame groundFlame;
    [Tooltip("불의 고리 반지름 (m, 둥지 중심 기준)")]
    [Min(1f)] public float fireRingRadius = 20f;
    [Tooltip("불의 고리에서 비워 둘 입구 각도 (도)")]
    [Range(0f, 180f)] public float fireRingGap = 40f;
    [Tooltip("불의 고리 불길 사이 간격 (m)")]
    [Min(0.5f)] public float fireRingSpacing = 2f;
    [Tooltip("불의 고리에서 몇 개마다 빛을 켤지")]
    [Min(1)] public int fireRingLightEvery = 4;
    [Tooltip("불의 고리에 닿았을 때 밀려나는 거리 (m)")]
    [Min(0f)] public float fireRingKnockback = 3f;

    [Header("효과음 (피격·사망·발견은 위의 몬스터 효과음 칸, 패턴 소리는 각 패턴 에셋)")]
    [Tooltip("2페이즈가 되어 몸과 불의 고리에 불이 붙을 때")]
    public SfxCue phaseCue;
    [Tooltip("불의 고리가 타는 소리 (빛을 켠 불길에서만 반복)")]
    public SfxCue fireRingLoopCue;
    [Tooltip("제단에 공물을 바쳐 보스를 다시 부를 때")]
    public SfxCue summonCue;

    [Header("낮/밤")]
    [Tooltip("밤에 이동 속도 배율")]
    [Min(0f)] public float nightMoveScale = 0.85f;
    [Tooltip("밤에 패턴 사이 쉬는 시간 배율")]
    [Min(0f)] public float nightGapScale = 1.5f;
    [Tooltip("밤에 주는 피해 배율")]
    [Min(0f)] public float nightDamageScale = 0.85f;
}
