using UnityEngine;

// 조명 오버레이 연출 값 (어둠 색, 광원 수, 밝기 계단)
[CreateAssetMenu(fileName = "LightingSetting", menuName = "Common/LightingSetting")]
public class LightingSetting : ScriptableObject
{
    // 셰이더(LightingOverlay.shader)의 MAX_LIGHTS와 같아야 함
    public const int MaxLights = 32;

    [Tooltip("밤 진행도(0 = 낮, 1 = 한밤)에 따른 어둠 색. 화면 색에 곱해짐 (흰색 = 변화 없음)")]
    public Gradient darknessGradient = new()
    {
        colorKeys = new[]
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(new Color(0.12f, 0.14f, 0.25f), 1f),
        },
    };

    [Tooltip("한 번에 그리는 최대 광원 수 (카메라에 가까운 순)")]
    [Range(1, MaxLights)] public int maxVisibleLights = MaxLights;

    [Tooltip("밝기를 이 단계 수로 끊어서 픽셀풍 계단으로 표현 (0 = 부드럽게)")]
    [Range(0, 16)] public int lightSteps = 0;

    [Tooltip("빛을 계산할 지면 높이 (y)")]
    public float groundHeight = 0f;
}
