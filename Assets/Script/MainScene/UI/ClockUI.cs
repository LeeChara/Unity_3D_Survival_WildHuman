using UnityEngine;

// 낮밤 원판을 현재 시간에 맞춰 회전
// 원판 스프라이트: 위쪽 절반 = 낮, 아래쪽 절반 = 밤 (시계 틀의 위쪽 창으로만 보임)
// 낮 한가운데에 낮 면이 정확히 위를 향하고, 시계 방향으로 돌며 밤 면이 올라옴
// 마우스를 올리면 '3일차 14:30'처럼 일차와 게임 내 시각을 표시
public class ClockUI : MonoBehaviour
{
    [SerializeField] private DayNightCycle dayNightCycle;
    [SerializeField] private RectTransform dial;

    [Header("마우스 호버 글자")]
    [Tooltip("마우스를 올리면 '일차 시각' 표시 (없어도 됨)")]
    [SerializeField] private HoverTextTrigger hoverText;
    [Tooltip("낮이 시작되는 게임 내 시각 (시)")]
    [SerializeField, Range(0, 23)] private int dayStartHour = 6;
    [Tooltip("밤이 시작되는 게임 내 시각 (시)")]
    [SerializeField, Range(0, 23)] private int nightStartHour = 18;
    [Tooltip("분을 이 단위로 끊어서 표시 (게임 시간이 빨리 흘러 숫자가 너무 자주 바뀌지 않도록)")]
    [SerializeField, Range(1, 60)] private int minuteStep = 10;

    private const int MinutesPerDay = 24 * 60;

    // 마지막으로 글자에 반영한 값
    private int lastDay = -1;
    private int lastMinutes = -1;

    private void Update()
    {
        // 낮: 90° → -90°, 밤: -90° → -270°
        // 낮과 밤 길이가 달라도 페이즈 경계에서 원판이 정확히 반씩 돌도록 페이즈 단위로 계산
        float startAngle = dayNightCycle.IsNight ? -90f : 90f;
        float angle = startAngle - 180f * dayNightCycle.PhaseProgress;

        dial.localRotation = Quaternion.Euler(0f, 0f, angle);

        UpdateHoverText();
    }

    // 원판과 같은 기준(페이즈 진행도)으로 시각을 계산해서, 밤이 시작되는 순간이 정확히 nightStartHour가 되게 함
    private void UpdateHoverText()
    {
        if (hoverText == null) return;

        int dayStart = dayStartHour * 60;
        int nightStart = nightStartHour * 60;
        int dayLength = ((nightStart - dayStart) % MinutesPerDay + MinutesPerDay) % MinutesPerDay;
        int nightLength = MinutesPerDay - dayLength;

        bool isNight = dayNightCycle.IsNight;
        float minutes = (isNight ? nightStart : dayStart)
            + (isNight ? nightLength : dayLength) * dayNightCycle.PhaseProgress;

        int shown = Mathf.FloorToInt(minutes / minuteStep) * minuteStep % MinutesPerDay;
        int day = dayNightCycle.DayCount;
        if (shown == lastMinutes && day == lastDay) return;

        lastMinutes = shown;
        lastDay = day;
        hoverText.SetText($"{day}일차 {shown / 60:00}:{shown % 60:00}");
    }
}
