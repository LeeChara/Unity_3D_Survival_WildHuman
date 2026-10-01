using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 인벤토리 한 칸의 표시 (아이콘 + 개수)
// 마우스 드래그 이벤트는 받아서 InventoryUI에 칸 번호와 함께 넘기기만 함
// 이벤트를 받으려면 루트에 Raycast Target이 켜진 Graphic(투명 Image)이 있어야 함
public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countText;

    private InventoryUI owner;
    private int index;

    public void Bind(InventoryUI owner, int index)
    {
        this.owner = owner;
        this.index = index;
    }

    public void Set(ItemStack stack)
    {
        bool hasItem = !stack.IsEmpty;

        icon.enabled = hasItem;
        icon.sprite = hasItem ? stack.item.icon : null;

        // 1개일 때는 개수를 표시하지 않음
        countText.text = hasItem && stack.count > 1 ? stack.count.ToString() : string.Empty;
    }

    public void OnBeginDrag(PointerEventData eventData) => owner.BeginDrag(index, eventData);
    public void OnDrag(PointerEventData eventData) => owner.Drag(eventData);
    public void OnEndDrag(PointerEventData eventData) => owner.EndDrag(eventData);
    public void OnDrop(PointerEventData eventData) => owner.Drop(index, eventData);
}
