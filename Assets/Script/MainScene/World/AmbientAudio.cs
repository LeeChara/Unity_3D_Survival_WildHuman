using UnityEngine;
using UnityEngine.Audio;

// 환경음: 낮(바람·새)과 밤(풀벌레) 소리를 밤 정도에 따라 섞어 반복 재생, 비가 오면 작아짐
// 낮↔밤이 바뀌는 순간의 알림음과 큰비 중의 천둥도 여기서 냄
// 환경음 음량은 설정의 '환경음'(GameOptions.AmbientVolume)을 곱하고 Master 믹서 그룹으로 출력 (RainAudio와 같음)
public class AmbientAudio : MonoBehaviour
{
    [SerializeField] private DayNightCycle dayNight;
    [Tooltip("비가 한창일 때 환경음이 줄어드는 정도")]
    [SerializeField, Range(0f, 1f)] private float rainDucking = 0.6f;

    private AudioSource daySource;
    private AudioSource nightSource;
    private float thunderTimer;

    private void Awake()
    {
        if (dayNight == null) dayNight = FindAnyObjectByType<DayNightCycle>();

        AudioMixerGroup master = null;
        var mixer = Resources.Load<AudioMixer>("Audio/MainMixer");
        if (mixer != null)
        {
            AudioMixerGroup[] groups = mixer.FindMatchingGroups("Master");
            if (groups.Length > 0) master = groups[0];
        }
        daySource = CreateSource(master);
        nightSource = CreateSource(master);
    }

    private void OnEnable()
    {
        if (dayNight != null) dayNight.PhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        if (dayNight != null) dayNight.PhaseChanged -= OnPhaseChanged;
    }

    private AudioSource CreateSource(AudioMixerGroup group)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.outputAudioMixerGroup = group;
        return source;
    }

    private void Update()
    {
        SfxSetting setting = SfxSetting.Instance;
        if (setting == null) return;

        WeatherSystem weather = WeatherSystem.Instance;
        float rain = weather != null ? weather.RainFactor : 0f;
        float night = dayNight != null ? dayNight.NightFactor : 0f;
        float level = setting.ambientVolume * GameOptions.AmbientVolume * (1f - rainDucking * rain);
        bool paused = Time.timeScale <= 0f;

        Drive(daySource, setting.ambientDay, level * (1f - night), paused);
        Drive(nightSource, setting.ambientNight, level * night, paused);
        UpdateThunder(setting, rain, paused);
    }

    private static void Drive(AudioSource source, SfxCue cue, float volume, bool paused)
    {
        AudioClip clip = cue != null ? cue.FirstClip : null;
        if (clip == null || volume <= 0.001f)
        {
            if (source.isPlaying) source.Stop();
            return;
        }

        if (source.clip != clip)
        {
            source.clip = clip;
            source.Stop();
        }
        source.volume = volume * cue.volume;

        if (paused)
        {
            if (source.isPlaying) source.Pause();
            return;
        }
        if (!source.isPlaying) source.UnPause();
        if (!source.isPlaying) source.Play();
    }

    // 큰비가 오는 동안 가끔 천둥 (비가 그치면 다음 비를 위해 다시 기다림)
    private void UpdateThunder(SfxSetting setting, float rain, bool paused)
    {
        if (paused) return;
        if (rain < setting.thunderRainFactor)
        {
            thunderTimer = Random.Range(setting.thunderInterval.x * 0.3f, setting.thunderInterval.x);
            return;
        }

        thunderTimer -= Time.deltaTime;
        if (thunderTimer > 0f) return;
        thunderTimer = Random.Range(setting.thunderInterval.x, setting.thunderInterval.y);
        Sfx.PlayUI(setting.thunder, Random.Range(0.6f, 1f));
    }

    private void OnPhaseChanged(bool isNight)
    {
        SfxSetting setting = SfxSetting.Instance;
        if (setting != null) Sfx.PlayUI(isNight ? setting.dusk : setting.dawn);
    }
}
