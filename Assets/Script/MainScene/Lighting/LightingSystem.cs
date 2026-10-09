using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 조명 오버레이 관리
// 시간대에 맞는 어둠 색과 카메라 근처 광원 목록을 셰이더 전역값으로 넘기고 (LightingOverlay.shader가 화면에 곱함)
// 같은 계산으로 월드 위치의 밝기를 조회할 수 있게 함 (어둠 패널티, 몬스터 습성 등에서 사용)
public class LightingSystem : MonoBehaviour
{
    private static readonly int AmbientLightId = Shader.PropertyToID("_OverlayAmbient");
    private static readonly int LightPosRadiusId = Shader.PropertyToID("_OverlayLightPosRadius");
    private static readonly int LightColorId = Shader.PropertyToID("_OverlayLightColor");
    private static readonly int LightCountId = Shader.PropertyToID("_OverlayLightCount");
    private static readonly int LightStepsId = Shader.PropertyToID("_OverlaySteps");
    private static readonly int GroundHeightId = Shader.PropertyToID("_OverlayGroundHeight");
    // 화면 모서리 순서: 왼쪽 아래, 오른쪽 아래, 왼쪽 위, 오른쪽 위
    private static readonly Vector2[] Corners = { new(0f, 0f), new(1f, 0f), new(0f, 1f), new(1f, 1f) };
    private static readonly int[] NearCornerIds =
    {
        Shader.PropertyToID("_OverlayNearBL"), Shader.PropertyToID("_OverlayNearBR"),
        Shader.PropertyToID("_OverlayNearTL"), Shader.PropertyToID("_OverlayNearTR"),
    };
    private static readonly int[] FarCornerIds =
    {
        Shader.PropertyToID("_OverlayFarBL"), Shader.PropertyToID("_OverlayFarBR"),
        Shader.PropertyToID("_OverlayFarTL"), Shader.PropertyToID("_OverlayFarTR"),
    };
    private static readonly int LightingEnabledId = Shader.PropertyToID("_OverlayEnabled");

    // 광원이 시스템보다 먼저 켜질 수 있어서 정적 목록으로 둠
    private static readonly List<LightSource> sources = new();

    [SerializeField] private LightingSetting setting;
    [SerializeField] private DayNightCycle dayNightCycle;

    private readonly Vector4[] posRadius = new Vector4[LightingSetting.MaxLights];
    private readonly Vector4[] colors = new Vector4[LightingSetting.MaxLights];
    private readonly List<LightSource> visible = new();
    private Camera cam;

    public static LightingSystem Instance { get; private set; }

    // 현재 어둠 색 (흰색 = 낮)
    public Color Ambient { get; private set; } = Color.white;

    public static void Register(LightSource source)
    {
        if (!sources.Contains(source)) sources.Add(source);
    }

    public static void Unregister(LightSource source)
    {
        sources.Remove(source);
    }

    // 셰이더와 같은 감쇠: 중심 1 → 반경 끝 0
    public static float Falloff(float distance, float radius)
    {
        if (radius <= 0f) return 0f;
        float t = 1f - Mathf.Clamp01(distance / radius);
        return t * t * (3f - 2f * t);
    }

    // 월드 위치의 밝기 (0 = 완전한 어둠, 1 이상 = 낮만큼 밝음)
    // safeOnly면 기본 시야처럼 안전하지 않은 광원은 빼고 계산
    public float GetLightLevel(Vector3 position, bool safeOnly = false)
    {
        float lights = 0f;
        foreach (LightSource source in sources)
        {
            if (safeOnly && !source.CountsAsSafe) continue;

            Vector3 offset = position - source.Position;
            float distance = new Vector2(offset.x, offset.z).magnitude;
            lights += Falloff(distance, source.CurrentRadius) * source.CurrentIntensity;
        }

        return Mathf.Max(Ambient.grayscale, lights);
    }

    private void Awake()
    {
        Instance = this;
        cam = Camera.main;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    // 씬을 떠난 뒤 다른 씬 화면이 어두워지지 않도록 오버레이를 끔
    private void OnDisable()
    {
        Shader.SetGlobalFloat(LightingEnabledId, 0f);
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        Ambient = setting.darknessGradient.Evaluate(dayNightCycle.NightFactor);
    }

    // 카메라 이동(LateUpdate)이 끝난 뒤 그리기 직전에 넘겨서 빛이 한 프레임 밀리지 않게 함
    // 씬 뷰 등 다른 카메라는 오버레이를 끔
    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera rendering)
    {
        if (cam == null) cam = Camera.main;
        bool isGameCamera = rendering == cam;
        Shader.SetGlobalFloat(LightingEnabledId, isGameCamera ? 1f : 0f);
        if (!isGameCamera) return;

        UploadCamera();
        UploadLights();

        Shader.SetGlobalColor(AmbientLightId, Ambient);
        Shader.SetGlobalFloat(LightStepsId, setting.lightSteps);
        Shader.SetGlobalFloat(GroundHeightId, setting.groundHeight);
    }

    // 화면 네 모서리의 near·far 점을 넘겨서 셰이더가 픽셀마다 지면 위치를 구하게 함
    // 광선 시작점까지 넘기므로 원근·직교 카메라 모두 지원
    private void UploadCamera()
    {
        float near = cam.nearClipPlane;
        for (int i = 0; i < Corners.Length; i++)
        {
            Vector2 c = Corners[i];
            Shader.SetGlobalVector(NearCornerIds[i], cam.ViewportToWorldPoint(new Vector3(c.x, c.y, near)));
            Shader.SetGlobalVector(FarCornerIds[i], cam.ViewportToWorldPoint(new Vector3(c.x, c.y, near + 1f)));
        }
    }

    // 화면 중심이 바라보는 지면에서 가까운 광원부터 최대 개수만큼
    private void UploadLights()
    {
        Vector3 focus = GroundPoint(cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)));

        visible.Clear();
        visible.AddRange(sources);
        if (visible.Count > setting.maxVisibleLights)
        {
            visible.Sort((a, b) => EdgeDistance(a, focus).CompareTo(EdgeDistance(b, focus)));
            visible.RemoveRange(setting.maxVisibleLights, visible.Count - setting.maxVisibleLights);
        }

        for (int i = 0; i < visible.Count; i++)
        {
            LightSource source = visible[i];
            Vector3 pos = source.Position;
            posRadius[i] = new Vector4(pos.x, pos.z, source.CurrentRadius, source.CurrentIntensity);
            colors[i] = source.Color;
        }

        Shader.SetGlobalVectorArray(LightPosRadiusId, posRadius);
        Shader.SetGlobalVectorArray(LightColorId, colors);
        Shader.SetGlobalInt(LightCountId, visible.Count);
    }

    private Vector3 GroundPoint(Ray ray)
    {
        if (Mathf.Abs(ray.direction.y) < 0.0001f) return ray.origin;
        float t = (setting.groundHeight - ray.origin.y) / ray.direction.y;
        return ray.GetPoint(Mathf.Max(0f, t));
    }

    // 빛 테두리까지의 거리 (반경이 큰 광원은 멀어도 화면에 걸칠 수 있음)
    private static float EdgeDistance(LightSource source, Vector3 focus)
    {
        Vector3 offset = source.Position - focus;
        return new Vector2(offset.x, offset.z).magnitude - source.CurrentRadius;
    }
}
