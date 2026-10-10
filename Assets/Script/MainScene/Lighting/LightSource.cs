using UnityEngine;

// 조명 오버레이에 원형 빛을 더하는 광원 (모닥불, 플레이어 기본 시야 등)
// 활성화되어 있는 동안 LightingSystem에 등록됨
public class LightSource : MonoBehaviour
{
    [Tooltip("빛이 닿는 반경 (월드 단위)")]
    [SerializeField] private float radius = 5f;
    [Tooltip("중심 밝기 (1 = 낮과 같은 밝기)")]
    [SerializeField] private float intensity = 1f;
    [SerializeField] private Color color = Color.white;

    [Header("깜빡임")]
    [Tooltip("반경·밝기가 흔들리는 비율 (0 = 깜빡이지 않음)")]
    [SerializeField, Range(0f, 0.5f)] private float flickerAmount = 0f;
    [SerializeField] private float flickerSpeed = 4f;

    [Tooltip("어둠 판정에서 안전한 빛으로 칠지 (플레이어 기본 시야는 false)")]
    [SerializeField] private bool countsAsSafe = true;

    // 광원마다 깜빡임이 겹치지 않도록 노이즈 시작점을 다르게 둠
    private float noiseSeed;

    public Color Color => color;
    public bool CountsAsSafe => countsAsSafe;
    public Vector3 Position => transform.position;

    // -1 ~ 1
    private float Flicker => flickerAmount > 0f
        ? (Mathf.PerlinNoise(Time.time * flickerSpeed, noiseSeed) - 0.5f) * 2f * flickerAmount
        : 0f;

    public float CurrentRadius => radius * (1f + Flicker);
    public float CurrentIntensity => intensity * (1f + Flicker * 0.5f);

    private void Awake()
    {
        noiseSeed = Random.value * 100f;
    }

    // 실행 중에 수치를 바꿀 때 사용 (예: 손에 든 랜턴의 빛)
    public void Configure(float radius, float intensity, Color color, float flickerAmount)
    {
        this.radius = radius;
        this.intensity = intensity;
        this.color = color;
        this.flickerAmount = flickerAmount;
    }

    private void OnEnable()
    {
        LightingSystem.Register(this);
    }

    private void OnDisable()
    {
        LightingSystem.Unregister(this);
    }
}
