using System.Collections;
using UnityEngine;

// 보스 패턴 하나 (에셋 하나가 패턴 하나, 수치는 에셋에서 조정)
// 에셋은 여러 보스가 함께 쓸 수 있으므로 진행 상태는 BossAI에 두고, 패턴은 Run 코루틴 안의 지역 변수만 씀
// 실행 순서: BossAI가 조건(페이즈·거리·쿨타임)에 맞는 패턴을 가중치로 고름 → Run 실행 → 끝나면 patternGap만큼 쉼
// 기절·사망하면 BossAI가 코루틴을 멈추고 남은 범위 표시를 정리함
public abstract class BossPattern : ScriptableObject
{
    [Tooltip("도감·디버그용 이름")]
    public string displayName;

    [Header("사용 조건")]
    [Tooltip("1페이즈에서 사용")]
    public bool usableInPhase1 = true;
    [Tooltip("2페이즈에서 사용")]
    public bool usableInPhase2 = true;
    [Tooltip("목표와의 거리가 이 범위 안일 때만 사용 (m)")]
    [Min(0f)] public float minRange = 0f;
    [Min(0f)] public float maxRange = 4f;
    [Tooltip("다시 쓸 수 있을 때까지의 시간 (초, 패턴이 끝난 시점부터)")]
    [Min(0f)] public float cooldown = 0f;
    [Tooltip("후보 중에서 뽑힐 비중")]
    [Min(0f)] public float weight = 1f;
    [Tooltip("실행 중 피해를 받지 않음 (예: 포효)")]
    public bool invulnerable;

    public bool IsUsableInPhase(int phase) => phase >= 2 ? usableInPhase2 : usableInPhase1;

    // 거리 외에 추가 조건이 필요하면 재정의
    public virtual bool CanUse(BossAI boss)
    {
        return boss.TargetDistance >= minRange && boss.TargetDistance <= maxRange;
    }

    public abstract IEnumerator Run(BossAI boss);
}
