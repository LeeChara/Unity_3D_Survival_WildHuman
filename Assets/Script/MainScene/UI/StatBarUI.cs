using UnityEngine;
using UnityEngine.UI;

public class StatBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage; // Image Type = Filled, Fill Method = Horizontal

    public void Set(float current, float max)
    {
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        // 픽셀아트가 픽셀 중간에서 잘리지 않도록 스프라이트의 1px 단위로 끊어서 표시
        // 조금이라도 남아 있으면 최소 1px은 보이도록 올림 처리
        float pixelWidth = fillImage.sprite != null ? fillImage.sprite.rect.width : 0f;
        if (pixelWidth > 0f) ratio = Mathf.Ceil(ratio * pixelWidth) / pixelWidth;

        fillImage.fillAmount = ratio;
    }
}
