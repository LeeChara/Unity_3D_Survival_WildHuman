using UnityEngine;

// 상자를 우클릭하면 인벤토리와 함께 열리는 상자 창
// 칸 이동·Shift+클릭은 InventoryUI가 처리하고, 이 창은 칸 연결과 표시만 맡음
// 인벤토리를 닫거나, 상자에서 멀어지거나, 상자가 부서지면 닫힘
public class ChestUI : MonoBehaviour
{
    public static ChestUI Instance { get; private set; }

    // 이 스크립트는 항상 활성 상태인 부모에 두고, 패널만 켜고 끔
    [SerializeField] private RectTransform panel;
    [SerializeField] private InventoryUI inventoryUI;
    [Tooltip("상자 칸 (상자의 칸 수보다 많은 칸은 숨김)")]
    [SerializeField] private InventorySlotUI[] slotUIs;

    public bool IsOpen => chest != null;

    private StorageChest chest;
    private Transform player;

    private void Awake()
    {
        Instance = this;
        panel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        inventoryUI.OpenStateChanged += OnInventoryOpenStateChanged;
        inventoryUI.ExternalChanged += OnExternalChanged;
    }

    private void OnDisable()
    {
        inventoryUI.OpenStateChanged -= OnInventoryOpenStateChanged;
        inventoryUI.ExternalChanged -= OnExternalChanged;
    }

    public void Open(StorageChest target, Transform from)
    {
        if (target == null) return;
        if (chest != target) Close();

        chest = target;
        player = from;
        chest.SlotChanged += RefreshSlot;
        chest.SetOpen(true);

        for (int i = 0; i < slotUIs.Length; i++)
        {
            bool show = i < chest.SlotCount;
            slotUIs[i].gameObject.SetActive(show);
            if (!show) continue;

            slotUIs[i].Bind(inventoryUI, chest, i);
            slotUIs[i].Set(chest.Get(i));
        }

        // 제작 창이 External을 보고 숨을 수 있도록 인벤토리를 열기 전에 연결
        panel.gameObject.SetActive(true);
        inventoryUI.SetExternal(chest, panel);
        inventoryUI.SetOpen(true);
    }

    public void Close()
    {
        if (chest == null && !panel.gameObject.activeSelf) return;

        // 부서진 상자도 C# 객체는 남아 있으므로 이벤트 해제는 그대로 가능
        StorageChest closing = chest;
        if ((object)chest != null) chest.SlotChanged -= RefreshSlot;
        if (chest != null) chest.SetOpen(false);
        chest = null;
        player = null;

        // 다른 창이 칸 묶음을 이어받아 닫히는 경우에는 그 연결을 건드리지 않음
        if (inventoryUI.External == (ISlotContainer)closing) inventoryUI.SetExternal(null, null);
        panel.gameObject.SetActive(false);
    }

    private void Update()
    {
        if ((object)chest == null) return;

        if (chest == null || player == null || !chest.CanInteract(player.position)) Close();
    }

    private void OnInventoryOpenStateChanged(bool open)
    {
        if (!open) Close();
    }

    // 다른 창(재련기·상자)이 인벤토리와 함께 열리면 이 창은 닫음
    private void OnExternalChanged()
    {
        if ((object)chest != null && inventoryUI.External != (ISlotContainer)chest) Close();
    }

    private void RefreshSlot(int index)
    {
        if (index < slotUIs.Length) slotUIs[index].Set(chest.Get(index));
    }
}
