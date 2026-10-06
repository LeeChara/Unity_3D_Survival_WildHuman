using UnityEngine;
using UnityEngine.SceneManagement;

// 씬 전환·게임 종료를 한 곳에서 처리 (씬 이름 중복 방지)
public static class SceneFlow
{
    public const string TitleScene = "TitleScene";
    public const string MainScene = "MainScene";

    public static void LoadTitle() => Load(TitleScene);
    public static void LoadMain() => Load(MainScene);

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 Play 모드 종료
#else
        Application.Quit(); // 빌드된 게임에서는 애플리케이션 자체를 종료
#endif
    }

    // 일시정지 상태에서 넘어가도 다음 씬이 멈춘 채 시작되지 않도록 시간 배율 복구
    private static void Load(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
