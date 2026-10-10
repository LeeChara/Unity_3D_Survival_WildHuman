using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 우클릭 시 핫바에서 선택한 아이템을 사용하고, 사용되면 1개 소모
// 커서가 가까운 상호작용 대상(모닥불 등)을 가리키면 아이템 사용 대신 상호작용
public class PlayerItemUser : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerHotbar hotbar;
    [SerializeField] private PlayerState playerState;
    [Tooltip("상호작용 대상을 찾을 레이어 (설치물)")]
    [SerializeField] private LayerMask interactLayers;
    [SerializeField] private float interactRayDistance = 100f;

    private Vector2 pointerScreenPos;

    // 아이템 사용에 성공한 시점을 이펙트 등 외부 시스템에 알림 (사용한 아이템)
    public event Action<ItemData> ItemUsed;

    void OnPoint(InputValue value)
    {
        pointerScreenPos = value.Get<Vector2>();
    }

    void OnUse(InputValue value)
    {
        // 인벤토리 칸 위의 우클릭은 아이템 나눠 옮기기용이므로 무시
        if (PointerUtil.IsOverUI(pointerScreenPos)) return;
        // 회피·공격·넉백 중에는 사용 불가
        if (!playerState.CanAct) return;

        // 상호작용 대상 위에서는 들고 있는 아이템을 먹거나 설치하지 않음 (빈손이어도 회수 가능)
        if (TryGetInteractable(out IInteractable interactable))
        {
            interactable.Interact(inventory, hotbar);
            return;
        }

        ItemStack stack = hotbar.SelectedStack;
        if (stack.IsEmpty) return;

        // 설치물이 커서가 가리키는 지면에 놓이도록 실제 지면 높이 기준으로 계산
        if (!PointerUtil.TryGetGroundPoint(pointerScreenPos, PointerUtil.GroundHeight, out Vector3 aimPoint))
        {
            aimPoint = new Vector3(transform.position.x, PointerUtil.GroundHeight, transform.position.z);
        }

        // 마지막 1개를 쓰면 Remove에서 칸이 비워지므로 미리 보관
        ItemData item = stack.item;
        if (item.TryUse(new ItemUseContext(gameObject, aimPoint)))
        {
            inventory.Remove(hotbar.SelectedIndex, 1);
            ItemUsed?.Invoke(item);
        }
    }

    // 커서 광선이 처음 닿은 대상이 상호작용 거리 안이면 true
    private bool TryGetInteractable(out IInteractable interactable)
    {
        interactable = null;
        if (Camera.main == null) return false;

        Ray ray = Camera.main.ScreenPointToRay(pointerScreenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRayDistance, interactLayers, QueryTriggerInteraction.Ignore)) return false;

        interactable = hit.collider.GetComponentInParent<IInteractable>();
        return interactable != null && interactable.CanInteract(transform.position);
    }
}
