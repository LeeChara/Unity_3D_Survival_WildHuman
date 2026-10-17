using UnityEngine;

// 방어력과 공격 피해의 비율로 받는 피해 배율을 정하는 공식
// 방어력이 피해의 1.5배가 될 때마다 "한 단계 위"로 보고(d), 단계 차이를 곡선에 넣음
// 알몸(방어력 0)이면 피해를 그대로 받고, 방어력 = 피해일 때 20%를 받음
// d가 -1 → 0일 때 피해가 크게 꺾이고(시그모이드 계단), 그 뒤로는 단계마다 일정 비율로 줄어듦(r^d)
public static class EquipmentDefense
{
    // 방어력이 이만큼 곱해질 때마다 한 단계로 봄
    private const float StepRatio = 1.5f;

    // 단계마다 줄어드는 비율
    private const float DecayRate = 0.85f;
    // 방어력이 모자랄 때 추가로 받는 피해의 크기
    private const float KneeSize = 0.8f;
    // 꺾임의 날카로움
    private const float KneeSharpness = 6f;

    // 곡선의 범위 (최댓값으로 나눠 알몸일 때 1이 되게 함)
    private const float MinCurve = 0.1f;
    private const float MaxCurve = 5f;

    public static float Multiplier(float defense, float damage)
    {
        if (defense <= 0f || damage <= 0f) return 1f;

        float d = Mathf.Log(defense / damage, StepRatio);
        return Curve(d) / MaxCurve;
    }

    private static float Curve(float d)
    {
        float m = Mathf.Pow(DecayRate, d) * (1f + KneeSize * Knee(d)) / (1f + KneeSize * Knee(0f));
        return Mathf.Clamp(m, MinCurve, MaxCurve);
    }

    // 방어력이 피해보다 0.5단계 이상 낮으면 1, 높으면 0에 가까움
    private static float Knee(float d)
    {
        return 1f / (1f + Mathf.Exp(KneeSharpness * (d + 0.5f)));
    }
}
