using UnityEngine;
using UnityEngine.InputSystem;

// 몬스터·자원·설치물 위에 마우스를 올리면 '이름 현재/최대 체력'을 HoverTextUI로 표시 (예: 방패멧돼지 20/50)
// 커서 광선이 닿은 콜라이더의 Health에서 이름과 체력을 읽음
// 땅의 아이템 툴팁이 떠 있으면 겹치지 않도록 표시하지 않음
// 들고 있는 도구로 캘 수 없는 자원이면 이름 뒤에 '(채굴 불가능)'을 덧붙임
// 상호작용 대상이면 다음 줄에 지금 우클릭하면 할 동작을 표시 (마우스 프리뷰, 예: 굽기 (날고기))
public class WorldHoverText : MonoBehaviour
{
    [Tooltip("이름·체력을 표시할 대상 레이어 (몬스터, 자원, 설치물)")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private float maxDistance = 100f;
    [Tooltip("이 툴팁이 떠 있으면 양보 (없어도 됨)")]
    [SerializeField] private WorldItemHover itemHover;
    [Tooltip("들고 있는 도구로 채굴 가능 여부를 판정 (비워두면 표시하지 않음)")]
    [SerializeField] private PlayerHotbar hotbar;
    [Tooltip("마우스 프리뷰: 상호작용 대상을 찾는 판정을 공유 (비워두면 동작 줄을 표시하지 않음)")]
    [SerializeField] private PlayerItemUser itemUser;

    // 같은 내용이면 매 프레임 글자를 다시 만들지 않도록 마지막으로 표시한 내용
    private Health shownTarget;
    private int shownCurrent;
    private int shownMax;
    private bool shownBlocked;
    private string shownPreview;
    // 대상이 파괴되면 shownTarget이 null로 취급되므로 표시 여부는 따로 기록
    private bool showing;

    private void OnDisable()
    {
        Clear();
    }

    // WorldItemHover가 Update에서 이번 프레임의 표시 여부를 정한 뒤에 판정
    private void LateUpdate()
    {
        if (!TryGetScreenPos(out Vector2 screenPos))
        {
            Clear();
            return;
        }

        bool hasTarget = TryGetHoveredTarget(screenPos, out Health target);
        string preview = GetInteractPreview(screenPos);
        if (!hasTarget && preview == null)
        {
            Clear();
            return;
        }

        // 체력은 바와 같은 기준으로 조금이라도 남아 있으면 1로 보이도록 올림
        int current = hasTarget ? Mathf.CeilToInt(target.CurrentHealth) : 0;
        int max = hasTarget ? Mathf.CeilToInt(target.MaxHealth) : 0;
        bool blocked = hasTarget && IsHarvestBlocked(target);
        if (showing && target == shownTarget && current == shownCurrent && max == shownMax && blocked == shownBlocked
            && preview == shownPreview) return;

        showing = true;
        shownTarget = target;
        shownCurrent = current;
        shownMax = max;
        shownBlocked = blocked;
        shownPreview = preview;
        if (HoverTextUI.Instance != null) HoverTextUI.Instance.Show(this, BuildText(target, current, max, blocked, preview));
    }

    // 첫 줄은 '이름 체력', 둘째 줄은 지금 우클릭하면 할 동작 (둘 중 하나만 있으면 그 줄만)
    private static string BuildText(Health target, int current, int max, bool blocked, string preview)
    {
        string info = null;
        if (target != null)
        {
            string suffix = blocked ? " (채굴 불가능)" : string.Empty;
            // 부서지지 않는 대상은 이름만
            info = target.IsInvulnerable ? $"{target.DisplayName}{suffix}" : $"{target.DisplayName}{suffix} {current}/{max}";
        }

        if (preview == null) return info;
        return info == null ? preview : $"{info}\n{preview}";
    }

    // 마우스 프리뷰: 커서 아래 상호작용 대상이 지금 할 동작 (거리와 상관없이 표시, 판정은 PlayerItemUser와 공유)
    private string GetInteractPreview(Vector2 screenPos)
    {
        if (itemUser == null || hotbar == null) return null;
        if (!itemUser.TryGetHoveredInteractable(screenPos, out IInteractable interactable)) return null;

        string preview = interactable.GetInteractPreview(hotbar);
        return string.IsNullOrEmpty(preview) ? null : preview;
    }

    // 자원이고 핫바에서 고른 도구(없으면 맨손)로 캘 수 없으면 true
    private bool IsHarvestBlocked(Health target)
    {
        if (hotbar == null || target is not PropHealth prop) return false;

        ToolItemData tool = hotbar.SelectedStack.IsEmpty ? null : hotbar.SelectedStack.item as ToolItemData;
        return tool != null ? !prop.CanHarvest(tool.toolType, tool.tier) : !prop.CanHarvest(ToolType.None, 0);
    }

    // 땅의 아이템 툴팁이 떠 있거나 UI 위면 표시하지 않음 (UI 쪽은 HoverTextTrigger 등이 처리)
    private bool TryGetScreenPos(out Vector2 screenPos)
    {
        screenPos = default;
        if (itemHover != null && itemHover.IsShowing) return false;
        if (Pointer.current == null || Camera.main == null) return false;

        screenPos = Pointer.current.position.ReadValue();
        return !PointerUtil.IsOverUI(screenPos);
    }

    private bool TryGetHoveredTarget(Vector2 screenPos, out Health target)
    {
        target = null;

        // 대상 레이어만 검사하므로 처음 닿은 것이 카메라에 가장 가까운 대상
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, targetLayers, QueryTriggerInteraction.Ignore)) return false;

        target = hit.collider.GetComponentInParent<Health>();
        return target != null && !target.IsDead && !string.IsNullOrEmpty(target.DisplayName);
    }

    private void Clear()
    {
        if (!showing) return;

        showing = false;
        shownTarget = null;
        shownPreview = null;
        if (HoverTextUI.Instance != null) HoverTextUI.Instance.Hide(this);
    }
}
