using UnityEngine;
using UnityEngine.UI;

public class StatBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage; // Image Type = Filled, Fill Method = Horizontal

    [Tooltip("마우스를 올렸을 때 값 앞에 붙는 이름 (예: 체력)")]
    [SerializeField] private string label;
    [Tooltip("마우스를 올리면 '이름 현재/최대' 표시 (없어도 됨)")]
    [SerializeField] private HoverTextTrigger hoverText;

    // 마지막으로 글자에 반영한 숫자
    private int lastCurrent = -1;
    private int lastMax = -1;

    public void Set(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        // 픽셀아트가 픽셀 중간에서 잘리지 않도록 스프라이트의 1px 단위로 끊어서 표시
        // 조금이라도 남아 있으면 최소 1px은 보이도록 올림 처리
        float pixelWidth = fillImage.sprite != null ? fillImage.sprite.rect.width : 0f;
        if (pixelWidth > 0f) ratio = Mathf.Ceil(ratio * pixelWidth) / pixelWidth;

        fillImage.fillAmount = ratio;

        // 바와 같은 기준으로 조금이라도 남아 있으면 1로 보이도록 올림
        // 허기처럼 매 프레임 바뀌는 값도 있으므로 숫자가 달라질 때만 글자를 새로 만듦
        int shownCurrent = Mathf.CeilToInt(current);
        int shownMax = Mathf.CeilToInt(max);
        if (hoverText != null && (shownCurrent != lastCurrent || shownMax != lastMax))
        {
            lastCurrent = shownCurrent;
            lastMax = shownMax;
            hoverText.SetText($"{label} {shownCurrent}/{shownMax}");
        }
    }
}
