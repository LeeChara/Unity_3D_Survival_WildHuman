using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// min~max 범위를 단계별로 고르는 설정 항목 (기본 0~100%). Slider의 값은 단계 번호(정수)로 두어 키보드 좌우 한 번에 한 단계씩 움직이게 함
[RequireComponent(typeof(Slider))]
public class OptionSlider : MonoBehaviour
{
    public enum Format { Percent, Multiplier }

    [SerializeField] private TMP_Text valueText;
    [SerializeField] private float min = 0f;
    [SerializeField] private float max = 1f;
    [Tooltip("min~max를 몇 단계로 나눌지 (0~1에서 20이면 5%씩)")]
    [SerializeField] private int steps = 20;
    [Tooltip("Percent: 80%, Multiplier: 1.3x")]
    [SerializeField] private Format format = Format.Percent;

    public event Action<float> ValueChanged;

    public Slider Slider { get; private set; }

    private void Awake()
    {
        Slider = GetComponent<Slider>();
        Slider.wholeNumbers = true;
        Slider.minValue = 0;
        Slider.maxValue = steps;
        Slider.onValueChanged.AddListener(OnSliderChanged);
    }

    // min~max
    public void SetValueWithoutNotify(float value)
    {
        Slider.SetValueWithoutNotify(Mathf.Round(Mathf.InverseLerp(min, max, value) * steps));
        UpdateText();
    }

    private void OnSliderChanged(float step)
    {
        UpdateText();
        ValueChanged?.Invoke(Value);
    }

    private float Value => Mathf.Lerp(min, max, Slider.value / steps);

    private void UpdateText()
    {
        if (valueText == null) return;
        valueText.text = format == Format.Percent ? $"{Mathf.RoundToInt(Value * 100f)}%" : $"{Value:0.0}x";
    }
}
