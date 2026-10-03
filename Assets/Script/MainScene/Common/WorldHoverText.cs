using UnityEngine;
using UnityEngine.InputSystem;

// 몬스터·자원·설치물 위에 마우스를 올리면 '이름 현재/최대 체력'을 HoverTextUI로 표시 (예: 방패멧돼지 20/50)
// 커서 광선이 닿은 콜라이더의 Health에서 이름과 체력을 읽음
// 땅의 아이템 툴팁이 떠 있으면 겹치지 않도록 표시하지 않음
public class WorldHoverText : MonoBehaviour
{
    [Tooltip("이름·체력을 표시할 대상 레이어 (몬스터, 자원, 설치물)")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private float maxDistance = 100f;
    [Tooltip("이 툴팁이 떠 있으면 양보 (없어도 됨)")]
    [SerializeField] private WorldItemHover itemHover;

    // 같은 내용이면 매 프레임 글자를 다시 만들지 않도록 마지막으로 표시한 내용
    private Health shownTarget;
    private int shownCurrent;
    private int shownMax;
    // 대상이 파괴되면 shownTarget이 null로 취급되므로 표시 여부는 따로 기록
    private bool showing;

    private void OnDisable()
    {
        Clear();
    }

    // WorldItemHover가 Update에서 이번 프레임의 표시 여부를 정한 뒤에 판정
    private void LateUpdate()
    {
        if (!TryGetHoveredTarget(out Health target))
        {
            Clear();
            return;
        }

        // 체력은 바와 같은 기준으로 조금이라도 남아 있으면 1로 보이도록 올림
        int current = Mathf.CeilToInt(target.CurrentHealth);
        int max = Mathf.CeilToInt(target.MaxHealth);
        if (showing && target == shownTarget && current == shownCurrent && max == shownMax) return;

        showing = true;
        shownTarget = target;
        shownCurrent = current;
        shownMax = max;
        if (HoverTextUI.Instance != null) HoverTextUI.Instance.Show(this, $"{target.DisplayName} {current}/{max}");
    }

    private bool TryGetHoveredTarget(out Health target)
    {
        target = null;
        if (itemHover != null && itemHover.IsShowing) return false;
        if (Pointer.current == null || Camera.main == null) return false;

        // UI 위에서는 UI 쪽(HoverTextTrigger 등)이 처리
        Vector2 screenPos = Pointer.current.position.ReadValue();
        if (PointerUtil.IsOverUI(screenPos)) return false;

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
        if (HoverTextUI.Instance != null) HoverTextUI.Instance.Hide(this);
    }
}
