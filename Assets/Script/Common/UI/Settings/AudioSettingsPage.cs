using UnityEngine;

// 사운드 탭: 전체 / 배경음 / 효과음 / 환경음 음량
public class AudioSettingsPage : SettingsPage
{
    [SerializeField] private OptionSlider masterSlider;
    [SerializeField] private OptionSlider musicSlider;
    [SerializeField] private OptionSlider sfxSlider;
    [SerializeField] private OptionSlider ambientSlider;

    private void Awake()
    {
        masterSlider.ValueChanged += GameOptions.SetMasterVolume;
        musicSlider.ValueChanged += GameOptions.SetMusicVolume;
        sfxSlider.ValueChanged += GameOptions.SetSfxVolume;
        ambientSlider.ValueChanged += GameOptions.SetAmbientVolume;
    }

    public override void Refresh()
    {
        masterSlider.SetValueWithoutNotify(GameOptions.MasterVolume);
        musicSlider.SetValueWithoutNotify(GameOptions.MusicVolume);
        sfxSlider.SetValueWithoutNotify(GameOptions.SfxVolume);
        ambientSlider.SetValueWithoutNotify(GameOptions.AmbientVolume);
    }

    public override void ResetToDefault()
    {
        GameOptions.SetMasterVolume(GameOptions.DefaultVolume);
        GameOptions.SetMusicVolume(GameOptions.DefaultVolume);
        GameOptions.SetSfxVolume(GameOptions.DefaultVolume);
        GameOptions.SetAmbientVolume(GameOptions.DefaultVolume);
        Refresh();
    }
}
