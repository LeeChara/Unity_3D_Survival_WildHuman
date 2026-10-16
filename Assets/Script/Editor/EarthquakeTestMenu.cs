using UnityEditor;
using UnityEngine;

// 지진 시험용 메뉴 (플레이 모드, 개발용)
// EarthquakeEvent 컴포넌트 메뉴의 '지금 지진 발생'과 같지만 오브젝트를 고르지 않고 바로 실행
public static class EarthquakeTestMenu
{
    [MenuItem("Tools/지진/지금 지진 발생")]
    private static void Trigger() => Object.FindFirstObjectByType<EarthquakeEvent>().Trigger();

    [MenuItem("Tools/지진/지금 지진 발생", true)]
    private static bool CanTrigger() => Application.isPlaying && Object.FindFirstObjectByType<EarthquakeEvent>() != null;
}
