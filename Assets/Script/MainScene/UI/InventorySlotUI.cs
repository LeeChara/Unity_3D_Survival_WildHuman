using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 인벤토리 한 칸의 표시 (아이콘 + 개수)
// 마우스 드래그·Shift+클릭 이벤트는 받아서 InventoryUI에 자기 자신(칸 묶음 + 칸 번호)과 함께 넘기기만 함
// 이벤트를 받으려면 루트에 Raycast Target이 켜진 Graphic(투명 Image)이 있어야 함
public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countText;
    [Tooltip("개수 글자 뒤에 상하좌우 1px씩 어긋나게 깐 검은 글자 (픽셀 외곽선)")]
    [SerializeField] private TMP_Text[] countOutlines;
    [Tooltip("마우스를 올리면 아이템 정보 표시 (드래그 미리보기 칸은 비워 둠)")]
    [SerializeField] private ItemTooltipTrigger tooltip;

    private InventoryUI owner;

    // 이 칸이 보여 주는 칸 묶음과 그 안의 칸 번호
    public ISlotContainer Container { get; private set; }
    public int Index { get; private set; }

    public void Bind(InventoryUI owner, ISlotContainer container, int index)
    {
        this.owner = owner;
        Container = container;
        Index = index;
    }

    public void Set(ItemStack stack)
    {
        bool hasItem = !stack.IsEmpty;

        icon.enabled = hasItem;
        icon.sprite = hasItem ? stack.item.icon : null;

        // 1개일 때는 개수를 표시하지 않음
        string count = hasItem && stack.count > 1 ? stack.count.ToString() : string.Empty;
        countText.text = count;
        foreach (var outline in countOutlines)
        {
            outline.text = count;
        }

        if (tooltip != null) tooltip.SetItem(hasItem ? stack.item : null, stack.count);
    }

    // 인벤토리가 닫혀 있을 때 핫바 칸에서는 툴팁을 띄우지 않음
    public void SetTooltipEnabled(bool value)
    {
        if (tooltip != null) tooltip.enabled = value;
    }

    public void OnBeginDrag(PointerEventData eventData) => owner.BeginDrag(this, eventData);
    public void OnDrag(PointerEventData eventData) => owner.Drag(eventData);
    public void OnEndDrag(PointerEventData eventData) => owner.EndDrag(eventData);
    public void OnDrop(PointerEventData eventData) => owner.Drop(this, eventData);

    // Shift+좌클릭: 묶음 전체를 반대편(재련기 ↔ 인벤토리, 또는 핫바 ↔ 가방)으로 옮김
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || owner == null) return;
        if (Keyboard.current == null || !Keyboard.current.shiftKey.isPressed) return;
        owner.QuickMove(this);
    }
}
