using UnityEngine;

// 게임플레이 탭: 언어, 화면 흔들림 세기, 마우스 오버 글자 표시, 공격 범위 이펙트 표시, 화면 효과 표시, 비 효과 표시, 파티클 양
public class GameplaySettingsPage : SettingsPage
{
    private static readonly string[] OnOff = { "끄기", "켜기" };
    // GameOptions.ParticleLevel 값 순서
    private static readonly string[] ParticleLevels = { "끄기", "적게", "보통" };
    // GameOptions.Languages와 같은 순서
    private static readonly string[] LanguageNames = { "한국어" };

    [SerializeField] private OptionSelector languageSelector;
    [SerializeField] private OptionSlider screenShakeSlider;
    [SerializeField] private OptionSelector hoverTextSelector;
    [SerializeField] private OptionSelector swingEffectSelector;
    [SerializeField] private OptionSelector screenEffectSelector;
    [SerializeField] private OptionSelector rainEffectSelector;
    [SerializeField] private OptionSelector particleSelector;

    private void Awake()
    {
        languageSelector.ValueChanged += index => GameOptions.SetLanguage(GameOptions.Languages[index]);
        screenShakeSlider.ValueChanged += GameOptions.SetScreenShake;
        hoverTextSelector.ValueChanged += index => GameOptions.SetShowHoverText(index == 1);
        swingEffectSelector.ValueChanged += index => GameOptions.SetShowSwingEffect(index == 1);
        screenEffectSelector.ValueChanged += index => GameOptions.SetShowScreenEffects(index == 1);
        rainEffectSelector.ValueChanged += index => GameOptions.SetShowRainEffect(index == 1);
        particleSelector.ValueChanged += GameOptions.SetParticleLevel;
    }

    public override void Refresh()
    {
        languageSelector.SetOptions(LanguageNames, System.Array.IndexOf(GameOptions.Languages, GameOptions.Language));
        screenShakeSlider.SetValueWithoutNotify(GameOptions.ScreenShake);
        hoverTextSelector.SetOptions(OnOff, GameOptions.ShowHoverText ? 1 : 0);
        swingEffectSelector.SetOptions(OnOff, GameOptions.ShowSwingEffect ? 1 : 0);
        screenEffectSelector.SetOptions(OnOff, GameOptions.ShowScreenEffects ? 1 : 0);
        rainEffectSelector.SetOptions(OnOff, GameOptions.ShowRainEffect ? 1 : 0);
        particleSelector.SetOptions(ParticleLevels, GameOptions.ParticleLevel);
    }

    public override void ResetToDefault()
    {
        // 언어는 사용자 환경이라 기본값으로 되돌리지 않음
        GameOptions.SetScreenShake(1f);
        GameOptions.SetShowHoverText(true);
        GameOptions.SetShowSwingEffect(true);
        GameOptions.SetShowScreenEffects(true);
        GameOptions.SetShowRainEffect(true);
        GameOptions.SetParticleLevel(2);
        Refresh();
    }
}
