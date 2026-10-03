using UnityEngine;
using UnityEngine.InputSystem;

// 땅에 떨어진 아이템 위에 마우스를 올리면 툴팁을 표시
// 아이템의 트리거 콜라이더는 수집용이라 작으므로, 반지름이 있는 SphereCast로 커서 주변까지 판정
public class WorldItemHover : MonoBehaviour
{
    [SerializeField] private LayerMask itemLayer;
    [Tooltip("커서 광선 주변으로 아이템을 찾는 반지름 (월드 단위)")]
    [SerializeField, Min(0f)] private float hoverRadius = 0.3f;
    [SerializeField] private float maxDistance = 100f;

    // 같은 내용이면 매 프레임 다시 그리지 않도록 마지막으로 표시한 내용
    private ItemData shownItem;
    private int shownCount;

    // 땅의 아이템 툴팁을 표시 중인지 (WorldHoverText가 겹치지 않도록 양보할 때 사용)
    public bool IsShowing => shownItem != null;

    private void OnDisable()
    {
        Clear();
    }

    private void Update()
    {
        if (!TryGetHoveredItem(out WorldItem item))
        {
            Clear();
            return;
        }

        if (item.Data == shownItem && item.Count == shownCount) return;

        shownItem = item.Data;
        shownCount = item.Count;
        if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.Show(this, shownItem, shownCount);
    }

    private bool TryGetHoveredItem(out WorldItem item)
    {
        item = null;
        if (Pointer.current == null || Camera.main == null) return false;

        // UI 위에서는 UI 쪽(ItemTooltipTrigger)이 처리
        Vector2 screenPos = Pointer.current.position.ReadValue();
        if (PointerUtil.IsOverUI(screenPos)) return false;

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (!Physics.SphereCast(ray, hoverRadius, out RaycastHit hit, maxDistance,
                itemLayer, QueryTriggerInteraction.Collide)) return false;

        item = hit.collider.GetComponentInParent<WorldItem>();
        return item != null;
    }

    private void Clear()
    {
        if (shownItem == null) return;

        shownItem = null;
        shownCount = 0;
        if (ItemTooltipUI.Instance != null) ItemTooltipUI.Instance.Hide(this);
    }
}
