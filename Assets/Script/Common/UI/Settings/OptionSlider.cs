using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 0~100%를 단계별로 고르는 설정 항목. Slider의 값은 단계 번호(정수)로 두어 키보드 좌우 한 번에 한 단계씩 움직이게 함
[RequireComponent(typeof(Slider))]
public class OptionSlider : MonoBehaviour
{
    [SerializeField] private TMP_Text valueText;
    [Tooltip("100%를 몇 단계로 나눌지 (20이면 5%씩)")]
    [SerializeField] private int steps = 20;

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

    // 0~1
    public void SetValueWithoutNotify(float value)
    {
        Slider.SetValueWithoutNotify(Mathf.Round(Mathf.Clamp01(value) * steps));
        UpdateText();
    }

    private void OnSliderChanged(float step)
    {
        UpdateText();
        ValueChanged?.Invoke(step / steps);
    }

    private void UpdateText()
    {
        if (valueText != null) valueText.text = $"{Mathf.RoundToInt(Slider.value / steps * 100f)}%";
    }
}
