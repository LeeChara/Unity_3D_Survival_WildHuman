using UnityEngine;

// 핫바에서 고른 아이템에 따라 손에 든 장비의 스프라이트를 교체
// 도구가 아니거나 빈 칸이면 아무것도 들지 않음
public class PlayerHeldItemVisual : MonoBehaviour
{
    [SerializeField] private PlayerHotbar hotbar;
    [SerializeField] private Inventory inventory;
    [Tooltip("손에 든 장비를 그리는 렌더러 (애니메이션이 위치·회전을 움직이는 오브젝트)")]
    [SerializeField] private SpriteRenderer heldRenderer;

    private void OnEnable()
    {
        hotbar.SelectionChanged += OnSelectionChanged;
        inventory.SlotChanged += OnSlotChanged;
    }

    // Inventory의 슬롯이 Awake에서 만들어진 뒤에 첫 외형을 맞춤
    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        hotbar.SelectionChanged -= OnSelectionChanged;
        inventory.SlotChanged -= OnSlotChanged;
    }

    private void OnSelectionChanged(int index)
    {
        Refresh();
    }

    // 선택한 칸의 아이템이 바뀐 경우(버리기·이동·획득 등)에도 외형을 갱신
    private void OnSlotChanged(int index)
    {
        if (index == hotbar.SelectedIndex) Refresh();
    }

    private void Refresh()
    {
        ItemStack stack = hotbar.SelectedStack;
        ToolItemData tool = stack.IsEmpty ? null : stack.item as ToolItemData;
        Sprite sprite = tool != null ? tool.heldSprite : null;

        heldRenderer.sprite = sprite;
        heldRenderer.enabled = sprite != null;
    }
}
