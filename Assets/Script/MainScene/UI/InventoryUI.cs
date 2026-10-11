using System;
using UnityEngine;
using UnityEngine.EventSystems;

// 같은 Canvas의 UI 위에서 굴린 휠은 부모를 타고 올라와 여기(IScrollHandler)로 들어옴
public class InventoryUI : MonoBehaviour, IScrollHandler
{
    // 이 스크립트는 항상 활성 상태인 부모에 두고, 패널만 켜고 끔
    [SerializeField] private GameObject panel;

    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerHotbar hotbar;
    [Tooltip("인벤토리 슬롯 순서와 같게 배치 (0~9 핫바, 10~39 가방)")]
    [SerializeField] private InventorySlotUI[] slotUIs;
    [Tooltip("선택된 핫바 칸으로 옮겨 다니는 강조 표시 (아이콘 뒤에 그려짐)")]
    [SerializeField] private RectTransform selectionHighlight;
    [Tooltip("드래그 중 마우스를 따라다니는 칸 (Canvas 맨 아래에 두어 가장 위에 그려지게 함, 레이캐스트 차단 끔)")]
    [SerializeField] private InventorySlotUI dragPreview;
    [SerializeField] private PlayerItemThrower thrower;
    [Tooltip("이 영역(핫바, 가방 줄) 안에서 놓으면 던지지 않음. 비활성(닫힌 가방)인 영역은 밖으로 취급")]
    [SerializeField] private RectTransform[] inventoryAreas;

    public bool IsOpen { get; private set; }
    public event Action<bool> OpenStateChanged;
    // 열린 인벤토리 영역 위에서 휠을 굴림 (양수: 위로, 음수: 아래로). 제작 창 선택 이동용
    public event Action<float> Scrolled;

    // 인벤토리와 함께 열린 다른 칸 묶음 (재련기 등, 없으면 null)과 그 창 영역
    public ISlotContainer External { get; private set; }
    private RectTransform externalArea;

    // 드래그 중인 칸 묶음·칸 번호 (-1이면 드래그 중 아님)와 들고 있는 개수
    private ISlotContainer dragContainer;
    private int dragFrom = -1;
    private int dragAmount;
    private bool droppedOnSlot;
    private readonly ItemStack previewStack = new();

    private void Awake()
    {
        for (int i = 0; i < slotUIs.Length; i++)
        {
            slotUIs[i].Bind(this, inventory, i);
        }

        // 시작 시 닫힌 상태로 통일
        SetOpen(false);
    }

    private void OnEnable()
    {
        inventory.SlotChanged += RefreshSlot;
        hotbar.SelectionChanged += MoveHighlight;
    }

    private void OnDisable()
    {
        // 재활성화 시 중복 등록되지 않도록 OnEnable과 짝을 맞춰 해제
        inventory.SlotChanged -= RefreshSlot;
        hotbar.SelectionChanged -= MoveHighlight;
    }

    private void Start()
    {
        // 초기 상태는 이벤트로 전달되지 않으므로 직접 한 번 반영
        for (int i = 0; i < slotUIs.Length; i++)
        {
            RefreshSlot(i);
        }
        MoveHighlight(hotbar.SelectedIndex);
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        // 가방 칸을 드래그하던 중 닫히면 칸이 비활성화되어 EndDrag가 오지 않으므로 직접 취소
        if (!open) CancelDrag();

        IsOpen = open;
        panel.SetActive(open);
        foreach (var slot in slotUIs)
        {
            slot.SetTooltipEnabled(open);
        }
        OpenStateChanged?.Invoke(open);
    }

    private void RefreshSlot(int index)
    {
        if (index >= slotUIs.Length) return;
        slotUIs[index].Set(inventory.Get(index));
    }

    // 강조 표시를 선택 칸의 자식 맨 앞으로 옮겨 칸 크기에 맞추고 아이콘 뒤에 그려지게 함
    private void MoveHighlight(int index)
    {
        selectionHighlight.SetParent(slotUIs[index].transform, false);
        selectionHighlight.SetAsFirstSibling();
    }

    // 재련기 창처럼 인벤토리와 함께 쓰는 칸 묶음을 연결 (null이면 해제)
    // 영역 안에서 놓으면 던지지 않음. 해제할 때 그 묶음의 칸을 드래그 중이었으면 취소
    public void SetExternal(ISlotContainer container, RectTransform area)
    {
        if (dragFrom >= 0 && dragContainer == External && External != container) CancelDrag();
        External = container;
        externalArea = container != null ? area : null;
    }

    // 좌클릭 드래그는 전부, 우클릭 드래그는 절반(올림)을 들어 올림
    public void BeginDrag(InventorySlotUI slot, PointerEventData eventData)
    {
        ItemStack stack = slot.Container != null ? slot.Container.Get(slot.Index) : null;
        bool validButton = eventData.button == PointerEventData.InputButton.Left
            || eventData.button == PointerEventData.InputButton.Right;

        if (stack == null || stack.IsEmpty || !validButton)
        {
            // 드래그 자체를 취소해 Drag·Drop·EndDrag가 오지 않게 함
            eventData.pointerDrag = null;
            return;
        }

        dragContainer = slot.Container;
        dragFrom = slot.Index;
        droppedOnSlot = false;
        dragAmount = eventData.button == PointerEventData.InputButton.Left
            ? stack.count
            : (stack.count + 1) / 2;

        previewStack.item = stack.item;
        previewStack.count = dragAmount;
        dragPreview.Set(previewStack);

        // 원래 칸과 같은 크기로 표시
        var previewRect = (RectTransform)dragPreview.transform;
        previewRect.sizeDelta = ((RectTransform)slot.transform).rect.size;
        previewRect.position = eventData.position;
        dragPreview.gameObject.SetActive(true);

        // 드래그 중에는 툴팁을 숨기고, 끝나면 그때 커서 아래 칸의 툴팁이 다시 보임
        if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.SetSuppressed(true);
    }

    public void Drag(PointerEventData eventData)
    {
        if (dragFrom < 0) return;
        dragPreview.transform.position = eventData.position;
    }

    // Drop은 놓은 칸에서 EndDrag보다 먼저 호출됨
    public void Drop(InventorySlotUI slot, PointerEventData eventData)
    {
        if (dragFrom < 0 || slot.Container == null) return;
        droppedOnSlot = true;
        SlotTransfer.Move(dragContainer, dragFrom, slot.Container, slot.Index, dragAmount);
    }

    // 칸에 놓이지 않았을 때: 인벤토리 영역 안이면 취소, 밖이면 들고 있던 만큼 던짐
    public void EndDrag(PointerEventData eventData)
    {
        if (dragFrom >= 0 && !droppedOnSlot && !IsOverInventoryArea(eventData.position))
        {
            ItemData item = dragContainer.Get(dragFrom).item;
            int removed = SlotTransfer.Remove(dragContainer, dragFrom, dragAmount);
            if (removed > 0) thrower.Throw(item, removed, eventData.position);
        }

        CancelDrag();
    }

    // Shift+클릭: 함께 열린 칸 묶음이 있으면 인벤토리 ↔ 그 묶음, 없으면 핫바 ↔ 가방으로 묶음 전체를 옮김
    public void QuickMove(InventorySlotUI slot)
    {
        if (!IsOpen || dragFrom >= 0 || slot.Container == null) return;

        int hotbarEnd = Mathf.Min(Inventory.SlotsPerRow, inventory.SlotCount);
        if (slot.Container != (ISlotContainer)inventory)
        {
            SlotTransfer.QuickMove(slot.Container, slot.Index, inventory, 0, inventory.SlotCount);
        }
        else if (External != null)
        {
            SlotTransfer.QuickMove(inventory, slot.Index, External, 0, External.SlotCount);
        }
        else if (slot.Index < hotbarEnd)
        {
            SlotTransfer.QuickMove(inventory, slot.Index, inventory, hotbarEnd, inventory.SlotCount);
        }
        else
        {
            SlotTransfer.QuickMove(inventory, slot.Index, inventory, 0, hotbarEnd);
        }
    }

    // 핫바 선택(PlayerHotbar)은 같은 조건일 때 휠을 무시하므로 둘이 동시에 움직이지 않음
    public void OnScroll(PointerEventData eventData)
    {
        float scroll = eventData.scrollDelta.y;
        if (!IsOpen || Mathf.Approximately(scroll, 0f)) return;
        if (!IsOverInventoryArea(eventData.position)) return;

        Scrolled?.Invoke(scroll);
    }

    // Screen Space - Overlay 캔버스이므로 카메라는 null
    public bool IsOverInventoryArea(Vector2 screenPos)
    {
        foreach (var area in inventoryAreas)
        {
            if (!area.gameObject.activeInHierarchy) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(area, screenPos, null)) return true;
        }
        return externalArea != null && externalArea.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(externalArea, screenPos, null);
    }

    private void CancelDrag()
    {
        dragContainer = null;
        dragFrom = -1;
        dragAmount = 0;
        droppedOnSlot = false;
        if (dragPreview != null) dragPreview.gameObject.SetActive(false);
        if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.SetSuppressed(false);
    }
}
