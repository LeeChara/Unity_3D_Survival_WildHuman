using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

// 플레이어가 설정 화면에서 바꾸는 값 (화면·사운드·게임플레이·언어). 바꾸는 즉시 적용하고 PlayerPrefs에 저장
// 게임 밸런스 값(GameSetting 에셋)과는 별개. 키 설정은 InputBindings가 담당
public static class GameOptions
{
    public const float DefaultVolume = 0.8f;
    // 지원 언어 코드. 번역은 아직 없고 한국어만 있음
    public static readonly string[] Languages = { "ko" };
    // 게임 HUD 배율 범위
    public const float MinHudScale = 0.5f;
    public const float MaxHudScale = 3f;

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
    // 게임 HUD 배율 (HudScaler가 적용)
    public static float HudScale { get; private set; }

    // 사운드 (0~1)
    public static float MasterVolume { get; private set; }
    public static float MusicVolume { get; private set; }
    public static float SfxVolume { get; private set; }
    // 환경음 (빗소리 등). 믹서 그룹이 없어서 재생하는 쪽이 직접 곱함 (RainAudio)
    public static float AmbientVolume { get; private set; }

    // 게임플레이
    // 화면 흔들림 세기 배율 (0~1). 흔들림 기능을 만들 때 이 값을 곱해서 사용
    public static float ScreenShake { get; private set; }
    // 몬스터·Prop·체력 바 등에 마우스를 올렸을 때 뜨는 글자 (HoverTextUI)
    public static bool ShowHoverText { get; private set; }
    // 공격 범위를 보여주는 휘두르기 이펙트 (SwingEffect)
    public static bool ShowSwingEffect { get; private set; }
    // 화면 가장자리 효과 (불타는 중 테두리 등)
    public static bool ShowScreenEffects { get; private set; }
    // 비가 올 때 화면 빗줄기 (RainOverlayUI). 꺼도 비 오는 날의 어두워짐은 남음
    public static bool ShowRainEffect { get; private set; }

    // 언어 코드 (Languages 중 하나)
    public static string Language { get; private set; }

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
        HudScale = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "GuiScale", 1f), MinHudScale, MaxHudScale);

        MasterVolume = PlayerPrefs.GetFloat(Prefix + "MasterVolume", DefaultVolume);
        MusicVolume = PlayerPrefs.GetFloat(Prefix + "MusicVolume", DefaultVolume);
        SfxVolume = PlayerPrefs.GetFloat(Prefix + "SfxVolume", DefaultVolume);
        AmbientVolume = PlayerPrefs.GetFloat(Prefix + "AmbientVolume", DefaultVolume);

        ScreenShake = PlayerPrefs.GetFloat(Prefix + "ScreenShake", 1f);
        ShowHoverText = GetBool("ShowHoverText", true);
        ShowSwingEffect = GetBool("ShowSwingEffect", true);
        ShowScreenEffects = GetBool("ShowScreenEffects", true);
        ShowRainEffect = GetBool("ShowRainEffect", true);

        Language = PlayerPrefs.GetString(Prefix + "Language", Languages[0]);
        if (Array.IndexOf(Languages, Language) < 0) Language = Languages[0];
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

    public static void SetHudScale(float value)
    {
        HudScale = Mathf.Clamp(value, MinHudScale, MaxHudScale);
        PlayerPrefs.SetFloat(Prefix + "GuiScale", HudScale);
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

    public static void SetAmbientVolume(float value)
    {
        AmbientVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(Prefix + "AmbientVolume", AmbientVolume);
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

    public static void SetShowScreenEffects(bool value)
    {
        ShowScreenEffects = value;
        SetBool("ShowScreenEffects", value);
        Save();
    }

    public static void SetShowRainEffect(bool value)
    {
        ShowRainEffect = value;
        SetBool("ShowRainEffect", value);
        Save();
    }

    // ---- 언어 ----

    public static void SetLanguage(string code)
    {
        if (Array.IndexOf(Languages, code) < 0) return;

        Language = code;
        PlayerPrefs.SetString(Prefix + "Language", code);
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
