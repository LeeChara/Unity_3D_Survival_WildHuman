using UnityEngine;

// 낮밤 원판을 현재 시간에 맞춰 회전
// 원판 스프라이트: 위쪽 절반 = 낮, 아래쪽 절반 = 밤 (시계 틀의 위쪽 창으로만 보임)
// 낮 한가운데에 낮 면이 정확히 위를 향하고, 시계 방향으로 돌며 밤 면이 올라옴
public class ClockUI : MonoBehaviour
{
    [SerializeField] private DayNightCycle dayNightCycle;
    [SerializeField] private RectTransform dial;

    private void Update()
    {
        // 낮: 90° → -90°, 밤: -90° → -270°
        // 낮과 밤 길이가 달라도 페이즈 경계에서 원판이 정확히 반씩 돌도록 페이즈 단위로 계산
        float startAngle = dayNightCycle.IsNight ? -90f : 90f;
        float angle = startAngle - 180f * dayNightCycle.PhaseProgress;

        dial.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
