using UnityEngine;
using UnityEngine.UI;

// 상태 아이콘 한 칸 (StatusIconsUI가 복제해서 사용)
public class StatusIconSlotUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [Tooltip("남은 시간 막대 (Image Type = Filled, Fill Method = Horizontal)")]
    [SerializeField] private Image remainingBar;
    [SerializeField] private HoverTextTrigger hoverText;

    private StatusEffectData data;
    private int shownSeconds = -1;

    public void Set(StatusEffectData effect, float remaining)
    {
        data = effect;
        icon.sprite = effect.icon;
        icon.enabled = effect.icon != null;
        shownSeconds = -1;
        SetRemaining(remaining);
    }

    public void SetRemaining(float remaining)
    {
        if (data == null) return;

        remainingBar.fillAmount = data.duration > 0f ? Mathf.Clamp01(remaining / data.duration) : 0f;

        // 남은 초가 바뀔 때만 글자를 새로 만듦
        int seconds = Mathf.CeilToInt(remaining);
        if (hoverText == null || seconds == shownSeconds) return;

        shownSeconds = seconds;
        string text = $"{data.displayName} · {seconds}초 남음";
        if (!string.IsNullOrEmpty(data.description)) text += $"\n{data.description}";
        hoverText.SetText(text);
    }
}
