using System.Collections.Generic;
using UnityEngine;

// 바닥 그림자 관리
// 시간대에 맞는 해 그림자 방향·길이를 계산해 등록된 모든 ShadowCaster에 한 번에 적용
// 진하기는 셰이더 전역값으로 넘겨서, 밤낮 전환 중에도 그림자 오브젝트를 건드리지 않고 함께 옅어지게 함
// 밤에는 그림자를 드리우는 광원 목록을 추려서 각 ShadowCaster가 광원 그림자를 갱신하게 함
// 낮에는 바람에 흘러가는 구름 그림자 값도 넘김 (지면은 LightingOverlay, 해 그림자는 GroundShadow가 CloudShadow.hlsl로 계산)
[DefaultExecutionOrder(100)]
public class ShadowSystem : MonoBehaviour
{
    private static readonly int SunAlphaId = Shader.PropertyToID("_ShadowSunAlpha");
    private static readonly int CloudTexId = Shader.PropertyToID("_CloudTex");
    private static readonly int CloudOffsetsId = Shader.PropertyToID("_CloudOffsets");
    private static readonly int CloudParamsId = Shader.PropertyToID("_CloudParams");

    // CloudShadow.hlsl의 CLOUD_SECOND_SCALE·CLOUD_FIRST_WEIGHT와 같아야 함
    private const float CloudSecondScale = 1.7f;
    private const float CloudFirstWeight = 0.65f;
    // 두 번째 구름 층은 조금 다른 방향·속도로 흘러서 모양이 계속 바뀜
    private const float CloudSecondYaw = 25f;
    private const float CloudSecondSpeed = 1.3f;

    // 그림자 오브젝트가 시스템보다 먼저 켜질 수 있어서 정적 목록으로 둠
    private static readonly List<ShadowCaster> casters = new();

    [SerializeField] private ShadowSetting setting;
    [SerializeField] private DayNightCycle dayNightCycle;
    [Tooltip("날마다 구름 양을 정할 월드 시드")]
    [SerializeField] private WorldState worldState;

    // 그림자가 뻗는 수평 방향
    public static Vector3 SunDirection { get; private set; } = Vector3.back;
    // 높이 1당 그림자 길이
    public static float SunLength { get; private set; } = 1f;
    // 그림자 진하기 (밤이면 0)
    public static float SunAlpha { get; private set; }
    // 지금 하늘을 덮는 비율 (0 = 맑음)
    public static float CloudCoverage { get; private set; }

    public static ShadowSystem Instance { get; private set; }

    // 구름 무늬의 현재 UV 이동량 (xy: 1층, zw: 2층)
    private Vector4 cloudOffsets;

    // 정지 오브젝트는 해가 이만큼 움직였을 때만 다시 계산 (매 프레임 수천 개를 옮기지 않도록)
    private const float StaticAngleStep = 0.5f;
    private const float StaticLengthStep = 0.01f;

    // 그림자를 드리우는 광원의 프레임 값 (밝기는 1을 넘지 않게 자름)
    public readonly struct ShadowLight
    {
        public readonly LightSource source;
        public readonly Vector3 position;
        public readonly float radius;
        public readonly float intensity;

        public ShadowLight(LightSource source, Vector3 position, float radius, float intensity)
        {
            this.source = source;
            this.position = position;
            this.radius = radius;
            this.intensity = intensity;
        }
    }

    // 이번 프레임에 그림자를 드리우는 광원
    private readonly List<ShadowLight> shadowLights = new();

    private Vector3 staticDirection;
    private float staticLength = -1f;

    public static void Register(ShadowCaster caster)
    {
        if (!casters.Contains(caster)) casters.Add(caster);
    }

    public static void Unregister(ShadowCaster caster)
    {
        casters.Remove(caster);
    }

    // 월드 위치의 구름 정도 (0 = 맑음, 1 = 구름 아래). 셰이더(CloudShadow.hlsl)와 같은 계산
    // 밤에도 구름 자체는 계속 흘러가므로 값이 나옴 (화면에서만 사라짐)
    public float GetCloudShade(Vector3 position)
    {
        if (CloudCoverage <= 0f || setting.cloudTexture == null) return 0f;

        float u = position.x / setting.cloudTileSize;
        float v = position.z / setting.cloudTileSize;
        float n = setting.cloudTexture.GetPixelBilinear(u + cloudOffsets.x, v + cloudOffsets.y).r * CloudFirstWeight
                + setting.cloudTexture.GetPixelBilinear(u * CloudSecondScale + cloudOffsets.z, v * CloudSecondScale + cloudOffsets.w).r * (1f - CloudFirstWeight);

        float edge = 1f - CloudCoverage;
        return SmoothStep(edge - setting.cloudSoftness, edge + setting.cloudSoftness, n);
    }

    private void Awake()
    {
        Instance = this;
    }

    // 씬을 떠난 뒤 남은 값이 다음 월드 첫 프레임에 보이지 않도록 초기화
    private void OnDisable()
    {
        SunAlpha = 0f;
        CloudCoverage = 0f;
        Shader.SetGlobalFloat(SunAlphaId, 0f);
        Shader.SetGlobalVector(CloudParamsId, Vector4.zero);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 이동(Update)이 끝난 뒤 위치를 따라가도록 늦게 실행
    private void LateUpdate()
    {
        UpdateSun();
        UpdateClouds();
        Shader.SetGlobalFloat(SunAlphaId, SunAlpha);
        bool sunStepped = CheckStaticStep();
        CollectShadowLights();
        float darkness = dayNightCycle.NightFactor;

        foreach (ShadowCaster caster in casters)
        {
            caster.Refresh(sunStepped);
            caster.RefreshLights(shadowLights, darkness);
        }
    }

    // 그림자를 드리우는 광원의 이번 프레임 값을 한 번만 읽어 둠
    private void CollectShadowLights()
    {
        shadowLights.Clear();
        foreach (LightSource source in LightingSystem.Sources)
        {
            if (!source.CastsShadows) continue;

            float radius = source.CurrentRadius;
            if (radius <= 0f) continue;

            shadowLights.Add(new ShadowLight(source, source.Position, radius, Mathf.Min(1f, source.CurrentIntensity)));
        }
    }

    private bool CheckStaticStep()
    {
        bool stepped = Vector3.Angle(staticDirection, SunDirection) > StaticAngleStep
            || Mathf.Abs(staticLength - SunLength) > StaticLengthStep;
        if (!stepped) return false;

        staticDirection = SunDirection;
        staticLength = SunLength;
        return true;
    }

    // 낮 진행도 0(아침) → 1(저녁)에 따라 방향은 아침 → 저녁으로 돌고, 길이는 정오에 가장 짧음
    // 밤에는 저녁 방향에 머문 채 사라지고, 아침이 밝아오며 다시 나타남
    private void UpdateSun()
    {
        float t = dayNightCycle.IsNight ? 1f : dayNightCycle.PhaseProgress;

        float yaw = Mathf.Lerp(setting.morningYaw, setting.eveningYaw, t) * Mathf.Deg2Rad;
        SunDirection = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        SunLength = Mathf.Lerp(setting.lowSunLength, setting.highSunLength, Mathf.Sin(t * Mathf.PI));
        SunAlpha = setting.alpha * (1f - dayNightCycle.NightFactor);
    }

    // 구름은 월드 누적 시간 기준으로 흘러서 불러온 뒤에도 같은 자리에서 이어짐
    private void UpdateClouds()
    {
        CloudCoverage = DailyCoverage();
        float time = dayNightCycle.WorldTime;
        float tile = setting.cloudTileSize;

        Vector2 first = WindDirection(setting.windYaw) * (setting.windSpeed * time / tile);
        Vector2 second = WindDirection(setting.windYaw + CloudSecondYaw) * (setting.windSpeed * CloudSecondSpeed * time * CloudSecondScale / tile);
        // 무늬가 바람 방향으로 흘러가도록 UV는 반대로 이동, 반복 무늬라 0~1로 접어서 큰 값의 정밀도 손실을 막음
        cloudOffsets = new Vector4(Mathf.Repeat(-first.x, 1f), Mathf.Repeat(-first.y, 1f), Mathf.Repeat(-second.x, 1f), Mathf.Repeat(-second.y, 1f));

        float darkness = setting.cloudDarkness * (1f - dayNightCycle.NightFactor);
        Shader.SetGlobalTexture(CloudTexId, setting.cloudTexture);
        Shader.SetGlobalVector(CloudOffsetsId, cloudOffsets);
        Shader.SetGlobalVector(CloudParamsId, new Vector4(1f / tile, setting.cloudTexture != null ? CloudCoverage : 0f, setting.cloudSoftness, darkness));
    }

    // 날짜마다 시드로 덮는 비율을 정하고, 하루 동안 다음 날 값으로 서서히 넘어감
    private float DailyCoverage()
    {
        int seed = worldState != null && worldState.Settings != null ? worldState.Settings.seed : 0;
        int day = dayNightCycle.DayCount;
        float t = Mathf.SmoothStep(0f, 1f, dayNightCycle.CycleProgress);
        return Mathf.Lerp(DayCoverage(seed, day), DayCoverage(seed, day + 1), t);
    }

    private float DayCoverage(int seed, int day)
    {
        return Mathf.Lerp(setting.minCoverage, setting.maxCoverage, Hash01(seed, day));
    }

    // 시드·날짜로 정해지는 0~1 값 (세이브를 불러와도 같은 날은 같은 하늘)
    private static float Hash01(int seed, int day)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u ^ (uint)day * 0x85EBCA77u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    private static Vector2 WindDirection(float yaw)
    {
        float rad = yaw * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
    }

    // HLSL smoothstep과 같은 식 (Mathf.SmoothStep은 인자 의미가 달라서 따로 둠)
    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
