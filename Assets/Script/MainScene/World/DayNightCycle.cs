using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// 낮 → 밤 → 낮 ... 순서로 시간을 진행
// 스프라이트가 Unlit이라 조명 대신 밤 전용 Volume(후처리)의 weight로 화면을 어둡게 함
public class DayNightCycle : MonoBehaviour, ISaveParticipant
{
    private const string SaveKey = "time";

    [SerializeField] private GameSetting gameSetting;

    // 밤 화면 톤을 담은 Volume (weight 0 = 낮, 1 = 밤)
    [SerializeField] private Volume nightVolume;

    // 인스펙터에서 현재 시간 확인용 (한 사이클 안에서 흐른 시간, 초)
    [SerializeField] private float cycleTime;

    private DayNightRule Rule => gameSetting.dayNight;
    private float CycleDuration => Rule.dayDuration + Rule.nightDuration;

    // 읽기 전용 프로퍼티
    public bool IsNight => cycleTime >= Rule.dayDuration;
    public int DayCount { get; private set; } = 1;
    // 현재 페이즈(낮 또는 밤) 안에서의 진행도 0~1
    public float PhaseProgress => IsNight
        ? (cycleTime - Rule.dayDuration) / Rule.nightDuration
        : cycleTime / Rule.dayDuration;

    // 낮↔밤 전환 시점을 외부 시스템에 알림 (밤이면 true)
    public event Action<bool> PhaseChanged;

    private void Start()
    {
        ApplyNightWeight();
    }

    private void Update()
    {
        bool wasNight = IsNight;

        cycleTime += Time.deltaTime;
        if (cycleTime >= CycleDuration)
        {
            cycleTime -= CycleDuration;
            DayCount++;
        }

        if (IsNight != wasNight) PhaseChanged?.Invoke(IsNight);

        ApplyNightWeight();
    }

    public void Capture(WorldSaveData data)
    {
        data.systems[SaveKey] = new JObject { ["cycleTime"] = cycleTime, ["dayCount"] = DayCount };
    }

    public void Restore(WorldSaveData data)
    {
        JToken saved = data.GetSystem(SaveKey);
        if (saved == null) return;

        cycleTime = Mathf.Clamp(saved.Value<float>("cycleTime"), 0f, CycleDuration);
        DayCount = Mathf.Max(1, saved.Value<int>("dayCount"));
        ApplyNightWeight();
    }

    // 각 페이즈 시작부터 transitionDuration 동안 서서히 전환
    private void ApplyNightWeight()
    {
        if (nightVolume == null) return;

        float elapsedInPhase = IsNight ? cycleTime - Rule.dayDuration : cycleTime;
        float t = Rule.transitionDuration > 0f ? Mathf.Clamp01(elapsedInPhase / Rule.transitionDuration) : 1f;

        // 첫 날 시작은 전환 없이 바로 낮
        if (!IsNight && DayCount == 1) t = 1f;

        nightVolume.weight = IsNight ? t : 1f - t;
    }
}
