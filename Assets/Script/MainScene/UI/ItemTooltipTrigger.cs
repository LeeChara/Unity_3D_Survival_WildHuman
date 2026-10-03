using UnityEngine;
using UnityEngine.EventSystems;

// UI 위에 마우스를 올리면 지정된 아이템의 툴팁을 표시 (인벤토리 칸, 레시피 칸, 재료 등에 붙여서 사용)
// 표시할 아이템은 이 칸을 그리는 스크립트가 SetItem으로 넣어 줌
// 이벤트를 받으려면 같은 오브젝트에 Raycast Target이 켜진 Graphic(투명 Image 등)이 있어야 함
// 컴포넌트를 끄면 이벤트를 받지 않음 (닫힌 인벤토리의 핫바 칸처럼 잠시 툴팁을 막을 때 사용)
public class ItemTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private ItemData item;
    private int count;
    private bool hovered;

    // count가 1 이하면 이름 뒤에 개수를 붙이지 않음
    public void SetItem(ItemData data, int amount)
    {
        item = data;
        count = amount;

        // 마우스를 올린 채 내용이 바뀐 경우 (드래그로 옮겨 놓음, 아이템 획득 등)
        if (hovered) Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        Refresh();
    }

    // 패널이 닫히면 PointerExit가 오지 않으므로 직접 정리
    private void OnDisable()
    {
        hovered = false;
        Refresh();
    }

    private void Refresh()
    {
        ItemTooltipUI tooltip = ItemTooltipUI.Instance;
        if (tooltip == null) return;

        if (hovered && item != null) tooltip.Show(this, item, count);
        else tooltip.Hide(this);
    }
}
