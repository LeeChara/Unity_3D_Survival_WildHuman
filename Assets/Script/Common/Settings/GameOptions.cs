using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

// 플레이어가 설정 화면에서 바꾸는 값 (화면·사운드·게임플레이). 바꾸는 즉시 적용하고 PlayerPrefs에 저장
// 게임 밸런스 값(GameSetting 에셋)과는 별개. 키 설정은 InputBindings가 담당
public static class GameOptions
{
    public const float DefaultVolume = 0.8f;

    // Resources 폴더 기준 경로. 노출 파라미터 이름은 아래 상수와 같아야 함
    private const string MixerPath = "Audio/MainMixer";
    private const string MasterParam = "MasterVolume";
    private const string MusicParam = "MusicVolume";
    private const string SfxParam = "SfxVolume";

    private const string Prefix = "Options.";

    // 어떤 값이든 바뀌면 호출 (표시 여부 옵션 등을 쓰는 쪽이 구독)
    public static event Action Changed;

    // 화면
    public static bool HasSavedDisplay { get; private set; }
    public static Vector2Int Resolution { get; private set; }
    public static FullScreenMode ScreenMode { get; private set; }
    public static bool VSync { get; private set; }
    // 0이면 제한 없음
    public static int FrameLimit { get; private set; }

    // 사운드 (0~1)
    public static float MasterVolume { get; private set; }
    public static float MusicVolume { get; private set; }
    public static float SfxVolume { get; private set; }

    // 게임플레이
    // 화면 흔들림 세기 배율 (0~1). 흔들림 기능을 만들 때 이 값을 곱해서 사용
    public static float ScreenShake { get; private set; }
    // 몬스터·Prop·체력 바 등에 마우스를 올렸을 때 뜨는 글자 (HoverTextUI)
    public static bool ShowHoverText { get; private set; }
    // 공격 범위를 보여주는 휘두르기 이펙트 (SwingEffect)
    public static bool ShowSwingEffect { get; private set; }

    private static AudioMixer mixer;

    // 첫 씬이 로드되기 전에 저장된 값을 불러와 적용
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        mixer = Resources.Load<AudioMixer>(MixerPath);
        Load();

        // 저장한 적이 없으면 프로젝트 설정의 화면 모드를 그대로 둠
        if (HasSavedDisplay) ApplyDisplay();
        ApplyFrameRate();
        ApplyAudio();

        // 믹서 값이 씬 로드 전에는 반영되지 않는 경우가 있어 씬마다 다시 적용
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyAudio();

    private static void Load()
    {
        HasSavedDisplay = PlayerPrefs.HasKey(Prefix + "Width");
        Resolution = new Vector2Int(
            PlayerPrefs.GetInt(Prefix + "Width", Screen.width),
            PlayerPrefs.GetInt(Prefix + "Height", Screen.height));
        ScreenMode = (FullScreenMode)PlayerPrefs.GetInt(Prefix + "ScreenMode", (int)Screen.fullScreenMode);
        VSync = GetBool("VSync", true);
        FrameLimit = PlayerPrefs.GetInt(Prefix + "FrameLimit", 0);

        MasterVolume = PlayerPrefs.GetFloat(Prefix + "MasterVolume", DefaultVolume);
        MusicVolume = PlayerPrefs.GetFloat(Prefix + "MusicVolume", DefaultVolume);
        SfxVolume = PlayerPrefs.GetFloat(Prefix + "SfxVolume", DefaultVolume);

        ScreenShake = PlayerPrefs.GetFloat(Prefix + "ScreenShake", 1f);
        ShowHoverText = GetBool("ShowHoverText", true);
        ShowSwingEffect = GetBool("ShowSwingEffect", true);
    }

    // ---- 화면 ----

    public static void SetDisplay(Vector2Int resolution, FullScreenMode mode)
    {
        Resolution = resolution;
        ScreenMode = mode;
        HasSavedDisplay = true;
        PlayerPrefs.SetInt(Prefix + "Width", resolution.x);
        PlayerPrefs.SetInt(Prefix + "Height", resolution.y);
        PlayerPrefs.SetInt(Prefix + "ScreenMode", (int)mode);
        ApplyDisplay();
        Save();
    }

    public static void SetVSync(bool value)
    {
        VSync = value;
        SetBool("VSync", value);
        ApplyFrameRate();
        Save();
    }

    public static void SetFrameLimit(int value)
    {
        FrameLimit = Mathf.Max(0, value);
        PlayerPrefs.SetInt(Prefix + "FrameLimit", FrameLimit);
        ApplyFrameRate();
        Save();
    }

    // ---- 사운드 ----

    public static void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(Prefix + "MasterVolume", MasterVolume);
        ApplyAudio();
        Save();
    }

    public static void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(Prefix + "MusicVolume", MusicVolume);
        ApplyAudio();
        Save();
    }

    public static void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(Prefix + "SfxVolume", SfxVolume);
        ApplyAudio();
        Save();
    }

    // ---- 게임플레이 ----

    public static void SetScreenShake(float value)
    {
        ScreenShake = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(Prefix + "ScreenShake", ScreenShake);
        Save();
    }

    public static void SetShowHoverText(bool value)
    {
        ShowHoverText = value;
        SetBool("ShowHoverText", value);
        Save();
    }

    public static void SetShowSwingEffect(bool value)
    {
        ShowSwingEffect = value;
        SetBool("ShowSwingEffect", value);
        Save();
    }

    // ---- 적용 ----

    private static void ApplyDisplay()
    {
        Screen.SetResolution(Resolution.x, Resolution.y, ScreenMode);
    }

    // 수직 동기화를 켜면 모니터 주사율을 따르므로 프레임 제한은 무시됨
    private static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = FrameLimit > 0 ? FrameLimit : -1;
    }

    private static void ApplyAudio()
    {
        if (mixer == null)
        {
            // 믹서가 없으면 전체 음량만 적용
            AudioListener.volume = MasterVolume;
            return;
        }

        mixer.SetFloat(MasterParam, ToDecibel(MasterVolume));
        mixer.SetFloat(MusicParam, ToDecibel(MusicVolume));
        mixer.SetFloat(SfxParam, ToDecibel(SfxVolume));
    }

    // 믹서 볼륨은 dB 단위. 0은 log를 쓸 수 없으므로 최소값(-80dB, 무음)으로 처리
    private static float ToDecibel(float volume)
    {
        return volume > 0.0001f ? Mathf.Log10(volume) * 20f : -80f;
    }

    private static void Save()
    {
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    private static bool GetBool(string key, bool defaultValue) => PlayerPrefs.GetInt(Prefix + key, defaultValue ? 1 : 0) != 0;
    private static void SetBool(string key, bool value) => PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
}
