using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 같은 Canvas의 UI 위에서 굴린 휠은 부모를 타고 올라와 여기(IScrollHandler)로 들어옴
public class InventoryUI : MonoBehaviour, IScrollHandler
{
    // 이 스크립트는 항상 활성 상태인 부모에 두고, 패널만 켜고 끔
    [SerializeField] private GameObject panel;

    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerHotbar hotbar;
    [Tooltip("인벤토리 슬롯 순서와 같게 배치 (0~9 핫바, 10~39 가방)")]
    [SerializeField] private InventorySlotUI[] slotUIs;
    [SerializeField] private PlayerEquipment equipment;
    [Tooltip("장비 칸 순서와 같게 배치 (0 머리, 1 몸, 2 유물). 장비 패널도 inventoryAreas에 넣어야 놓을 때 던지지 않음")]
    [SerializeField] private InventorySlotUI[] equipmentSlotUIs;
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

    // 인벤토리와 함께 열린 다른 칸 묶음 (재련기·상자 등, 없으면 null)과 그 창 영역
    public ISlotContainer External { get; private set; }
    // External이 바뀜 (제작 창은 다른 칸 묶음이 열려 있는 동안 숨음)
    public event Action ExternalChanged;
    private RectTransform externalArea;

    // 들고 있는 칸 묶음·칸 번호 (-1이면 들고 있지 않음)와 들고 있는 개수
    // 드래그로 들었는지, 클릭으로 커서에 들었는지(heldByClick)에 따라 놓는 방식만 다름
    private ISlotContainer dragContainer;
    private int dragFrom = -1;
    private int dragAmount;
    private bool droppedOnSlot;
    private bool heldByClick;
    private readonly ItemStack previewStack = new();

    // 마우스가 올라가 있는 칸 (숫자키로 핫바에 옮길 때, 클릭이 칸 밖인지 확인할 때 사용)
    private InventorySlotUI hovered;

    // 드래그 또는 클릭으로 아이템을 들고 있음 (이때 월드 클릭은 공격 대신 던지기)
    public bool IsHoldingItem => dragFrom >= 0;

    private void Awake()
    {
        for (int i = 0; i < slotUIs.Length; i++)
        {
            slotUIs[i].Bind(this, inventory, i);
        }
        for (int i = 0; i < equipmentSlotUIs.Length; i++)
        {
            equipmentSlotUIs[i].Bind(this, equipment, i);
        }

        // 시작 시 닫힌 상태로 통일
        SetOpen(false);
    }

    private void OnEnable()
    {
        inventory.SlotChanged += RefreshSlot;
        equipment.SlotChanged += RefreshEquipmentSlot;
        hotbar.SelectionChanged += MoveHighlight;
    }

    private void OnDisable()
    {
        // 재활성화 시 중복 등록되지 않도록 OnEnable과 짝을 맞춰 해제
        inventory.SlotChanged -= RefreshSlot;
        equipment.SlotChanged -= RefreshEquipmentSlot;
        hotbar.SelectionChanged -= MoveHighlight;
    }

    private void Start()
    {
        // 초기 상태는 이벤트로 전달되지 않으므로 직접 한 번 반영
        for (int i = 0; i < slotUIs.Length; i++)
        {
            RefreshSlot(i);
        }
        for (int i = 0; i < equipmentSlotUIs.Length; i++)
        {
            RefreshEquipmentSlot(i);
        }
        MoveHighlight(hotbar.SelectedIndex);
    }

    // 클릭으로 든 아이템은 드래그 이벤트가 없으므로 직접 커서를 따라가게 하고, 칸 밖 클릭을 처리
    // 칸 위 클릭은 그 칸의 OnPointerClick이 처리하므로 여기서는 칸 밖일 때만 처리
    private void Update()
    {
        if (!heldByClick || Pointer.current == null) return;

        Vector2 pointerPos = Pointer.current.position.ReadValue();
        dragPreview.transform.position = pointerPos;

        if (Mouse.current == null) return;
        bool released = Mouse.current.leftButton.wasReleasedThisFrame || Mouse.current.rightButton.wasReleasedThisFrame;
        if (!released || IsHoveringSlot()) return;

        // 인벤토리 영역 밖이면 들고 있던 만큼 던지고, 안쪽 빈 곳이면 내려놓기 취소
        if (!IsOverInventoryArea(pointerPos)) ThrowHeld(pointerPos);
        CancelDrag();
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        // 가방 칸을 드래그하던 중 닫히면 칸이 비활성화되어 EndDrag가 오지 않으므로 직접 취소
        if (!open)
        {
            CancelDrag();
            hovered = null;
        }

        IsOpen = open;
        panel.SetActive(open);
        foreach (var slot in slotUIs)
        {
            slot.SetTooltipEnabled(open);
        }
        foreach (var slot in equipmentSlotUIs)
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

    private void RefreshEquipmentSlot(int index)
    {
        if (index >= equipmentSlotUIs.Length) return;
        equipmentSlotUIs[index].Set(equipment.Get(index));
    }

    // 강조 표시를 선택 칸의 자식 맨 앞으로 옮겨 칸 크기에 맞추고 아이콘 뒤에 그려지게 함
    private void MoveHighlight(int index)
    {
        selectionHighlight.SetParent(slotUIs[index].transform, false);
        selectionHighlight.SetAsFirstSibling();
    }

    // 재련기·상자 창처럼 인벤토리와 함께 쓰는 칸 묶음을 연결 (null이면 해제)
    // 영역 안에서 놓으면 던지지 않음. 해제할 때 그 묶음의 칸을 드래그 중이었으면 취소
    public void SetExternal(ISlotContainer container, RectTransform area)
    {
        if (dragFrom >= 0 && dragContainer == External && External != container) CancelDrag();
        bool changed = External != container;
        External = container;
        externalArea = container != null ? area : null;
        if (changed) ExternalChanged?.Invoke();
    }

    // 좌클릭 드래그는 전부, 우클릭 드래그는 절반(올림)을 들어 올림
    public void BeginDrag(InventorySlotUI slot, PointerEventData eventData)
    {
        ItemStack stack = slot.Container != null ? slot.Container.Get(slot.Index) : null;
        bool validButton = eventData.button == PointerEventData.InputButton.Left
            || eventData.button == PointerEventData.InputButton.Right;

        // 클릭으로 이미 들고 있으면 드래그는 시작하지 않음 (놓는 것은 클릭으로 처리)
        if (stack == null || stack.IsEmpty || !validButton || dragFrom >= 0)
        {
            // 드래그 자체를 취소해 Drag·Drop·EndDrag가 오지 않게 함
            eventData.pointerDrag = null;
            return;
        }

        PickUp(slot, stack, eventData.button, eventData.position, false);
    }

    // 클릭: 들고 있지 않으면 커서에 들고, 들고 있으면 이 칸에 놓음 (원래 칸이면 내려놓기 취소)
    // 드래그를 원래 칸에서 놓아도 클릭이 먼저 오므로, 드래그로 든 경우는 무시하고 EndDrag에 맡김
    public void Click(InventorySlotUI slot, PointerEventData eventData)
    {
        if (!IsOpen || slot.Container == null) return;

        if (dragFrom < 0)
        {
            ItemStack stack = slot.Container.Get(slot.Index);
            bool validButton = eventData.button == PointerEventData.InputButton.Left
                || eventData.button == PointerEventData.InputButton.Right;
            if (stack.IsEmpty || !validButton) return;

            PickUp(slot, stack, eventData.button, eventData.position, true);
            return;
        }

        if (!heldByClick) return;

        if (slot.Container != dragContainer || slot.Index != dragFrom)
        {
            SlotTransfer.Move(dragContainer, dragFrom, slot.Container, slot.Index, dragAmount);
        }
        CancelDrag();
    }

    // 좌클릭은 전부, 우클릭은 절반(올림)을 들어 올림
    private void PickUp(InventorySlotUI slot, ItemStack stack, PointerEventData.InputButton button, Vector2 position, bool byClick)
    {
        dragContainer = slot.Container;
        dragFrom = slot.Index;
        droppedOnSlot = false;
        heldByClick = byClick;
        dragAmount = button == PointerEventData.InputButton.Left
            ? stack.count
            : (stack.count + 1) / 2;

        previewStack.item = stack.item;
        previewStack.count = dragAmount;
        dragPreview.Set(previewStack);

        // 원래 칸과 같은 크기로 표시
        var previewRect = (RectTransform)dragPreview.transform;
        previewRect.sizeDelta = ((RectTransform)slot.transform).rect.size;
        previewRect.position = position;
        dragPreview.gameObject.SetActive(true);

        // 들고 있는 동안에는 툴팁을 숨기고, 끝나면 그때 커서 아래 칸의 툴팁이 다시 보임
        if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.SetSuppressed(true);
    }

    public void SetHovered(InventorySlotUI slot) => hovered = slot;

    public void ClearHovered(InventorySlotUI slot)
    {
        if (hovered == slot) hovered = null;
    }

    // 닫힌 창의 칸은 Exit 없이 꺼질 수 있으므로 활성 상태까지 확인
    private bool IsHoveringSlot() => hovered != null && hovered.isActiveAndEnabled && hovered.Container != null;

    // 인벤토리가 열린 채 칸 위에서 숫자키: 그 칸과 해당 핫바 칸의 아이템을 옮기거나 맞바꿈
    // 처리했으면 true (이때 핫바 선택은 바꾸지 않음)
    public bool TryHotkeyMove(int hotbarIndex)
    {
        if (!IsOpen || dragFrom >= 0 || !IsHoveringSlot()) return false;
        if (hotbarIndex < 0 || hotbarIndex >= inventory.SlotCount) return false;

        ISlotContainer container = hovered.Container;
        int index = hovered.Index;
        ItemStack stack = container.Get(index);

        // 올린 칸이 비었으면 핫바 칸의 아이템을 이 칸으로 가져옴
        if (stack.IsEmpty)
        {
            ItemStack hotbarStack = inventory.Get(hotbarIndex);
            if (!hotbarStack.IsEmpty) SlotTransfer.Move(inventory, hotbarIndex, container, index, hotbarStack.count);
        }
        else
        {
            SlotTransfer.Move(container, index, inventory, hotbarIndex, stack.count);
        }
        return true;
    }

    public void Drag(PointerEventData eventData)
    {
        if (dragFrom < 0 || heldByClick) return;
        dragPreview.transform.position = eventData.position;
    }

    // Drop은 놓은 칸에서 EndDrag보다 먼저 호출됨
    public void Drop(InventorySlotUI slot, PointerEventData eventData)
    {
        if (dragFrom < 0 || heldByClick || slot.Container == null) return;
        droppedOnSlot = true;
        SlotTransfer.Move(dragContainer, dragFrom, slot.Container, slot.Index, dragAmount);
    }

    // 칸에 놓이지 않았을 때: 인벤토리 영역 안이면 취소, 밖이면 들고 있던 만큼 던짐
    public void EndDrag(PointerEventData eventData)
    {
        // 클릭으로 든 상태에서 끝난 드래그(시작 직후 취소된 경우 등)는 들고 있는 상태를 건드리지 않음
        if (heldByClick) return;

        if (dragFrom >= 0 && !droppedOnSlot && !IsOverInventoryArea(eventData.position))
        {
            ThrowHeld(eventData.position);
        }

        CancelDrag();
    }

    private void ThrowHeld(Vector2 screenPos)
    {
        ItemData item = dragContainer.Get(dragFrom).item;
        int removed = SlotTransfer.Remove(dragContainer, dragFrom, dragAmount);
        if (removed > 0) thrower.Throw(item, removed, screenPos);
    }

    // Shift+클릭: 함께 열린 칸 묶음이 있으면 인벤토리 ↔ 그 묶음, 없으면 핫바 ↔ 가방으로 묶음 전체를 옮김
    // 함께 열린 칸 묶음이 없을 때 인벤토리의 장비는 맞는 장비 칸에 장착 (이미 있으면 맞바꿈), 장비 칸은 인벤토리로 뺌
    public void QuickMove(InventorySlotUI slot)
    {
        if (!IsOpen || dragFrom >= 0 || slot.Container == null) return;

        int hotbarEnd = Mathf.Min(Inventory.SlotsPerRow, inventory.SlotCount);
        if (slot.Container != (ISlotContainer)inventory)
        {
            SlotTransfer.QuickMove(slot.Container, slot.Index, inventory, 0, inventory.SlotCount);
        }
        else if (External == null && slot.Container.Get(slot.Index).item is EquipmentItemData gear)
        {
            SlotTransfer.Move(inventory, slot.Index, equipment, (int)gear.slot, 1);
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
        heldByClick = false;
        if (dragPreview != null) dragPreview.gameObject.SetActive(false);
        if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.SetSuppressed(false);
    }
}
