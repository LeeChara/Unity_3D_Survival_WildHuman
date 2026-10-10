using UnityEngine;

// 핫바에서 골라 손에 들고 있는 동안 플레이어 주변을 밝히는 도구 (빛은 PlayerHeldLight가 켜고 끔)
// 공격 수치와 손에 든 스프라이트는 일반 도구와 같이 사용
[CreateAssetMenu(fileName = "LanternItemData", menuName = "Item/LanternItemData")]
public class LanternItemData : ToolItemData
{
    [Header("빛")]
    [Tooltip("빛이 닿는 반경 (월드 단위)")]
    [Min(0f)] public float lightRadius = 7f;
    [Tooltip("중심 밝기 (1 = 낮과 같은 밝기)")]
    [Min(0f)] public float lightIntensity = 2.5f;
    public Color lightColor = new Color(1f, 0.75f, 0.45f);
    [Tooltip("반경·밝기가 흔들리는 비율 (0 = 깜빡이지 않음)")]
    [Range(0f, 0.5f)] public float lightFlicker = 0.06f;

    public override string GetStatText()
    {
        return AppendElement($"빛 반경 {lightRadius:0.#} · {damage} 피해");
    }
}
