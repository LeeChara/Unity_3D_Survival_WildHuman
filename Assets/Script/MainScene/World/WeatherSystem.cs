using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

// 날씨 (지금은 비만)
// 월드 시간 기준으로 무작위 간격마다 비가 시작돼 정해진 시간 동안 내리다 그침 (모든 바이옴)
// 비가 오는 동안 로드된 젖을 수 있는 모든 것(Wettable)을 계속 적심 → 타던 것은 꺼지고, 불이 닿아도 마르기만 함
// 화면 어두워짐(LightingSystem)·빗줄기(RainOverlayUI)·빗소리(RainAudio)는 RainFactor를 보고 서서히 바뀜
// 다음 비·비가 그칠 시각은 월드 시간으로 저장
public class WeatherSystem : MonoBehaviour, ISaveParticipant
{
    private const string SaveKey = "weather";

    [SerializeField] private WeatherSetting setting;
    [SerializeField] private DayNightCycle clock;

    // 다음 비가 시작되는 월드 시간 (음수면 아직 정하지 않음)
    private float nextRainAt = -1f;
    // 지금 비의 시작·끝 월드 시간 (비가 오지 않으면 의미 없음)
    private float rainStartAt;
    private float rainEndAt;
    private float soakTimer;

    public static WeatherSystem Instance { get; private set; }

    public WeatherSetting Setting => setting;
    public bool IsRaining { get; private set; }
    // 비의 세기 0~1 (시작·끝 transitionDuration 동안 서서히 바뀜)
    public float RainFactor { get; private set; }
    // 비가 그칠 때까지 남은 시간(초, 비가 오지 않으면 0)
    public float RainRemaining => IsRaining ? Mathf.Max(0f, rainEndAt - clock.WorldTime) : 0f;

    // 비가 시작되거나 그친 시점 (비가 오면 true)
    public event Action<bool> RainChanged;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        float now = clock.WorldTime;

        if (IsRaining)
        {
            if (now >= rainEndAt) StopRain();
        }
        else if (nextRainAt < 0f) ScheduleNext();
        else if (now >= nextRainAt) StartRain(Random.Range(setting.rainDuration.x, setting.rainDuration.y));

        RainFactor = ComputeFactor(now);
        if (IsRaining) SoakAll();
    }

    // 테스트용: 지금 바로 비를 내리게 함
    [ContextMenu("비 시작")]
    public void StartRainNow() => StartRain(Random.Range(setting.rainDuration.x, setting.rainDuration.y));

    // 테스트용: 지금 바로 비를 그치게 함 (서서히 그침)
    [ContextMenu("비 그치기")]
    public void StopRainNow()
    {
        if (!IsRaining) return;
        float now = clock.WorldTime;
        rainEndAt = Mathf.Min(rainEndAt, now + setting.transitionDuration);
        // 시작 직후에 그치게 하면 세기가 0에서 다시 오르지 않도록 시작 시각을 앞당김
        rainStartAt = Mathf.Min(rainStartAt, now - setting.transitionDuration);
    }

    private void StartRain(float duration)
    {
        float now = clock.WorldTime;
        IsRaining = true;
        rainStartAt = now;
        rainEndAt = now + duration;
        soakTimer = 0f;
        RainChanged?.Invoke(true);
    }

    private void StopRain()
    {
        IsRaining = false;
        ScheduleNext();
        RainChanged?.Invoke(false);
    }

    private void ScheduleNext()
    {
        nextRainAt = clock.WorldTime + Random.Range(setting.rainInterval.x, setting.rainInterval.y);
    }

    private float ComputeFactor(float now)
    {
        if (!IsRaining) return 0f;
        if (setting.transitionDuration <= 0f) return 1f;

        float fadeIn = (now - rainStartAt) / setting.transitionDuration;
        float fadeOut = (rainEndAt - now) / setting.transitionDuration;
        return Mathf.Clamp01(Mathf.Min(fadeIn, fadeOut));
    }

    // 로드된 젖을 수 있는 모든 대상을 다시 적심 (젖음 시간이 계속 채워져서, 비가 그친 뒤부터 줄어듦)
    private void SoakAll()
    {
        soakTimer -= Time.deltaTime;
        if (soakTimer > 0f) return;
        soakTimer = setting.soakInterval;

        var targets = Wettable.Active;
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            if (i < targets.Count) targets[i].Soak();
        }
    }

    #region 저장

    public void Capture(WorldSaveData data)
    {
        var json = new JObject { ["next"] = nextRainAt, ["raining"] = IsRaining };
        if (IsRaining)
        {
            json["start"] = rainStartAt;
            json["end"] = rainEndAt;
        }
        data.systems[SaveKey] = json;
    }

    public void Restore(WorldSaveData data)
    {
        JToken saved = data.GetSystem(SaveKey);
        if (saved == null) return;

        nextRainAt = saved.Value<float>("next");
        IsRaining = saved.Value<bool>("raining");
        if (IsRaining)
        {
            rainStartAt = saved.Value<float>("start");
            rainEndAt = saved.Value<float>("end");
        }
    }

    #endregion
}
