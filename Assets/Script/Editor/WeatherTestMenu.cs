using UnityEditor;
using UnityEngine;

// 날씨 시험용 메뉴 (플레이 모드, 개발용)
public static class WeatherTestMenu
{
    [MenuItem("Tools/날씨/비 시작")]
    private static void StartRain() => WeatherSystem.Instance.StartRainNow();

    [MenuItem("Tools/날씨/비 그치기")]
    private static void StopRain() => WeatherSystem.Instance.StopRainNow();

    [MenuItem("Tools/날씨/비 시작", true)]
    [MenuItem("Tools/날씨/비 그치기", true)]
    private static bool CanUse() => Application.isPlaying && WeatherSystem.Instance != null;
}
