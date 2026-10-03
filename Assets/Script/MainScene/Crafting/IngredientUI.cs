using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 선택한 레시피의 재료 한 줄 (아이콘 + 보유/필요 개수, 부족하면 빨간 글자)
public class IngredientUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private Color enoughColor = Color.white;
    [SerializeField] private Color shortColor = new(1f, 0.35f, 0.35f);
    [Tooltip("마우스를 올리면 재료 정보 표시 (개수는 옆에 표시되므로 이름만)")]
    [SerializeField] private ItemTooltipTrigger tooltip;

    public void Set(ItemData item, int have, int need)
    {
        icon.sprite = item != null ? item.icon : null;
        amountText.text = $"{have}/{need}";
        amountText.color = have >= need ? enoughColor : shortColor;

        if (tooltip != null) tooltip.SetItem(item, 0);
    }
}
