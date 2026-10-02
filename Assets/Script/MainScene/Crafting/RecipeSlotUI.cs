using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 제작 창 아래 줄의 레시피 한 칸 (결과물 아이콘 + 개수, 제작 불가면 흐리게)
// 클릭하면 이 칸의 레시피를 선택 (이벤트를 받으려면 루트에 Raycast Target이 켜진 Graphic 필요)
public class RecipeSlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countText;
    [Tooltip("개수 글자 뒤에 상하좌우 1px씩 어긋나게 깐 검은 글자 (픽셀 외곽선)")]
    [SerializeField] private TMP_Text[] countOutlines;
    [Tooltip("제작할 수 없을 때 아이콘에 곱할 색")]
    [SerializeField] private Color unavailableTint = new(1f, 1f, 1f, 0.35f);

    private CraftingUI owner;
    private int offset;

    // offset: 가운데(선택) 칸 기준 상대 위치 (왼쪽 -, 오른쪽 +)
    public void Bind(CraftingUI owner, int offset)
    {
        this.owner = owner;
        this.offset = offset;
    }

    public void Set(Recipe recipe, bool craftable)
    {
        bool hasRecipe = recipe != null && recipe.result != null;

        icon.enabled = hasRecipe;
        icon.sprite = hasRecipe ? recipe.result.icon : null;
        icon.color = craftable ? Color.white : unavailableTint;

        // 1개를 만드는 레시피는 개수를 표시하지 않음
        string count = hasRecipe && recipe.resultCount > 1 ? recipe.resultCount.ToString() : string.Empty;
        countText.text = count;
        foreach (var outline in countOutlines)
        {
            outline.text = count;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        owner.MoveSelection(offset);
    }
}
