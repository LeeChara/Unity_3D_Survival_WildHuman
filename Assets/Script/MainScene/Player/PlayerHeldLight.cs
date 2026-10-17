using UnityEngine;

// 핫바에서 고른 아이템이 랜턴이거나 유물 칸에 랜턴을 끼고 있으면 빛을 켜고 랜턴의 수치를 적용 (손에 든 랜턴 우선)
// 둘 다 없으면 빛을 끔 (꺼진 LightSource는 LightingSystem에서 빠짐)
public class PlayerHeldLight : MonoBehaviour
{
    [SerializeField] private PlayerHotbar hotbar;
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerEquipment equipment;
    [Tooltip("랜턴을 들거나 끼었을 때만 켜지는 광원 (처음에는 꺼 둠)")]
    [SerializeField] private LightSource heldLight;

    private void OnEnable()
    {
        hotbar.SelectionChanged += OnSelectionChanged;
        inventory.SlotChanged += OnSlotChanged;
        if (equipment != null) equipment.SlotChanged += OnEquipmentChanged;
    }

    // Inventory의 슬롯이 Awake에서 만들어진 뒤에 첫 상태를 맞춤
    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        hotbar.SelectionChanged -= OnSelectionChanged;
        inventory.SlotChanged -= OnSlotChanged;
        if (equipment != null) equipment.SlotChanged -= OnEquipmentChanged;
    }

    private void OnSelectionChanged(int index)
    {
        Refresh();
    }

    // 선택한 칸의 아이템이 바뀐 경우(버리기·이동·획득 등)에도 갱신
    private void OnSlotChanged(int index)
    {
        if (index == hotbar.SelectedIndex) Refresh();
    }

    private void OnEquipmentChanged(int index)
    {
        Refresh();
    }

    private void Refresh()
    {
        ItemStack stack = hotbar.SelectedStack;
        LanternItemData lantern = stack.IsEmpty ? null : stack.item as LanternItemData;
        if (lantern == null && equipment != null) lantern = equipment.EquippedLantern;

        if (lantern != null)
        {
            heldLight.Configure(lantern.lightRadius, lantern.lightIntensity, lantern.lightColor, lantern.lightFlicker);
        }
        heldLight.enabled = lantern != null;
    }
}
