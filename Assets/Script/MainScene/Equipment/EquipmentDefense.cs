using UnityEngine;

// 방어력과 공격 피해로 받는 피해 배율을 정하는 공식
// 1구간: 방어력이 피해의 60%까지는 방어력만큼 그대로 뺌 (피해 − 방어력)
// 2구간: 그 뒤로는 남은 피해(피해의 40%)가 초과 방어력의 제곱근에 반비례해 줄어듦
//        → 방어력이 높아질수록 같은 방어력을 더해도 줄어드는 양이 점점 작아짐
// 두 구간은 경계에서 값과 기울기가 모두 이어지므로 체감이 끊기지 않음
// 결과는 방어력/피해 비율로만 정해지므로 수치가 커져도 모양이 같음
public static class EquipmentDefense
{
    // 피해 − 방어력을 그대로 적용하는 범위 (피해 대비 방어력 비율)
    private const float DirectRatio = 0.6f;

    public static float Multiplier(float defense, float damage)
    {
        if (defense <= 0f || damage <= 0f) return 1f;

        float ratio = defense / damage;
        if (ratio <= DirectRatio) return 1f - ratio;

        // 경계에서 남은 피해 비율과, 경계를 넘은 방어력 비율
        float remain = 1f - DirectRatio;
        float excess = ratio - DirectRatio;
        return remain / Mathf.Sqrt(1f + 2f * excess / remain);
    }
}
