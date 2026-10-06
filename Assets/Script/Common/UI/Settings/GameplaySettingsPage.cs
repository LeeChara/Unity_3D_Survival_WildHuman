using UnityEngine;

// 게임플레이 탭: 화면 흔들림 세기, 마우스 오버 글자 표시, 공격 범위 이펙트 표시
public class GameplaySettingsPage : SettingsPage
{
    private static readonly string[] OnOff = { "끄기", "켜기" };

    [SerializeField] private OptionSlider screenShakeSlider;
    [SerializeField] private OptionSelector hoverTextSelector;
    [SerializeField] private OptionSelector swingEffectSelector;

    private void Awake()
    {
        screenShakeSlider.ValueChanged += GameOptions.SetScreenShake;
        hoverTextSelector.ValueChanged += index => GameOptions.SetShowHoverText(index == 1);
        swingEffectSelector.ValueChanged += index => GameOptions.SetShowSwingEffect(index == 1);
    }

    public override void Refresh()
    {
        screenShakeSlider.SetValueWithoutNotify(GameOptions.ScreenShake);
        hoverTextSelector.SetOptions(OnOff, GameOptions.ShowHoverText ? 1 : 0);
        swingEffectSelector.SetOptions(OnOff, GameOptions.ShowSwingEffect ? 1 : 0);
    }

    public override void ResetToDefault()
    {
        GameOptions.SetScreenShake(1f);
        GameOptions.SetShowHoverText(true);
        GameOptions.SetShowSwingEffect(true);
        Refresh();
    }
}
