using UnityEngine;

// 바닥 그림자 연출 값 (해 방향·길이·진하기)
[CreateAssetMenu(fileName = "ShadowSetting", menuName = "Common/ShadowSetting")]
public class ShadowSetting : ScriptableObject
{
    // Default보다 먼저 그려지는 정렬 레이어 (세워진 스프라이트보다 항상 아래에 깔림)
    public const string SortingLayerName = "Shadow";
    // 오브젝트 하나가 받는 최대 광원 그림자 수 (가장 강한 순)
    public const int MaxLightShadows = 2;

    [Tooltip("그림자를 그릴 머티리얼 (GroundShadow.shader)")]
    public Material material;

    [Tooltip("낮 동안의 그림자 진하기 (밤으로 갈수록 0까지 사라짐)")]
    [Range(0f, 1f)] public float alpha = 0.35f;

    [Header("해")]
    [Tooltip("아침·저녁 그림자가 뻗는 방향 (월드 Y 각도). 카메라가 45도라 135 = 화면 오른쪽, 315 = 화면 왼쪽, 225 = 화면 아래")]
    public float morningYaw = 300f;
    public float eveningYaw = 150f;

    [Tooltip("높이 1당 그림자 길이: 아침·저녁(해가 낮을 때)")]
    [Min(0f)] public float lowSunLength = 1.4f;
    [Tooltip("높이 1당 그림자 길이: 정오(해가 높을 때)")]
    [Min(0f)] public float highSunLength = 0.25f;

    [Header("조명 그림자")]
    [Tooltip("광원 그림자를 그릴 머티리얼 (GroundShadow.shader, 해 진하기를 곱하지 않음)")]
    public Material lightMaterial;
    [Tooltip("광원 바로 옆, 한밤일 때의 진하기 (광원에서 멀어질수록, 낮이 될수록 옅어짐)")]
    [Range(0f, 1f)] public float lightAlpha = 0.85f;
    [Tooltip("높이 1당 그림자 길이: 광원 바로 옆")]
    [Min(0f)] public float nearLightLength = 0.3f;
    [Tooltip("높이 1당 그림자 길이: 빛 반경 끝")]
    [Min(0f)] public float farLightLength = 1.5f;

    [Header("구름 그림자")]
    [Tooltip("이음매 없이 반복되는 노이즈 (값이 0~1에 고르게 퍼져 있어야 덮는 비율 ≈ 구름 면적). CPU 조회를 위해 Read/Write 필요")]
    public Texture2D cloudTexture;
    [Tooltip("노이즈 한 장이 덮는 지면 크기 (m). 구름 하나는 대략 이 값의 1/4")]
    [Min(1f)] public float cloudTileSize = 160f;
    [Tooltip("바람이 부는 방향 (월드 Y 각도)")]
    public float windYaw = 60f;
    [Tooltip("구름이 흘러가는 속도 (m/초)")]
    [Min(0f)] public float windSpeed = 1.5f;
    [Tooltip("날마다 정해지는 하늘을 덮는 비율의 범위 (0 = 맑음). 하루 동안 다음 날 값으로 서서히 바뀜")]
    [Range(0f, 1f)] public float minCoverage = 0.1f;
    [Range(0f, 1f)] public float maxCoverage = 0.6f;
    [Tooltip("구름 가장자리가 퍼지는 정도")]
    [Range(0.01f, 0.5f)] public float cloudSoftness = 0.12f;
    [Tooltip("구름 아래 지면이 어두워지는 정도 (낮 기준, 밤에는 사라짐)")]
    [Range(0f, 1f)] public float cloudDarkness = 0.25f;

    [Header("지면")]
    [Tooltip("발밑 높이에서 살짝 띄워 지면과 겹쳐 깜빡이지 않게 함")]
    [Min(0f)] public float lift = 0.02f;
}
