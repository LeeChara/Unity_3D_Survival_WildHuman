using System.Collections.Generic;
using UnityEngine;

// 바닥 그림자 관리
// 시간대에 맞는 해 그림자 방향·길이를 계산해 등록된 모든 ShadowCaster에 한 번에 적용
// 진하기는 셰이더 전역값으로 넘겨서, 밤낮 전환 중에도 그림자 오브젝트를 건드리지 않고 함께 옅어지게 함
// 밤에는 그림자를 드리우는 광원 목록을 추려서 각 ShadowCaster가 광원 그림자를 갱신하게 함
[DefaultExecutionOrder(100)]
public class ShadowSystem : MonoBehaviour
{
    private static readonly int SunAlphaId = Shader.PropertyToID("_ShadowSunAlpha");

    // 그림자 오브젝트가 시스템보다 먼저 켜질 수 있어서 정적 목록으로 둠
    private static readonly List<ShadowCaster> casters = new();

    [SerializeField] private ShadowSetting setting;
    [SerializeField] private DayNightCycle dayNightCycle;

    // 그림자가 뻗는 수평 방향
    public static Vector3 SunDirection { get; private set; } = Vector3.back;
    // 높이 1당 그림자 길이
    public static float SunLength { get; private set; } = 1f;
    // 그림자 진하기 (밤이면 0)
    public static float SunAlpha { get; private set; }

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

    // 씬을 떠난 뒤 남은 값이 다음 월드 첫 프레임에 보이지 않도록 초기화
    private void OnDisable()
    {
        SunAlpha = 0f;
        Shader.SetGlobalFloat(SunAlphaId, 0f);
    }

    // 이동(Update)이 끝난 뒤 위치를 따라가도록 늦게 실행
    private void LateUpdate()
    {
        UpdateSun();
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
}
