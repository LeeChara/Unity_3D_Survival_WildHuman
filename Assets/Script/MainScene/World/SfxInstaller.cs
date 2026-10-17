using UnityEngine;
using UnityEngine.SceneManagement;

// 씬이 열릴 때 플레이어 효과음(PlayerSfx)과 환경음(AmbientAudio)을 붙임
// 씬 파일을 건드리지 않고 소리를 켜고 끌 수 있도록 코드로 붙임 (이미 붙어 있으면 그대로 둠)
public static class SfxInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayerHealth player = Object.FindAnyObjectByType<PlayerHealth>();
        if (player != null && !player.TryGetComponent(out PlayerSfx _)) player.gameObject.AddComponent<PlayerSfx>();

        DayNightCycle dayNight = Object.FindAnyObjectByType<DayNightCycle>();
        if (dayNight != null && !dayNight.TryGetComponent(out AmbientAudio _)) dayNight.gameObject.AddComponent<AmbientAudio>();
    }
}
