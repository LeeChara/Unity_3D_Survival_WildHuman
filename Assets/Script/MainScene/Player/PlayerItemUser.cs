using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 우클릭 시 핫바에서 선택한 아이템을 사용하고, 사용되면 1개 소모
// 커서가 상호작용 대상(모닥불 등)을 가리키면 아이템 사용 대신 상호작용 (멀면 걸어가서 상호작용)
public class PlayerItemUser : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerHotbar hotbar;
    [SerializeField] private PlayerState playerState;
    [Tooltip("상호작용 대상을 찾을 레이어 (설치물)")]
    [SerializeField] private LayerMask interactLayers;
    [SerializeField] private float interactRayDistance = 100f;
    [Tooltip("거리 밖의 상호작용 대상을 누르면 이걸로 걸어가서 상호작용")]
    [SerializeField] private PlayerMovement playerMovement;

    private Vector2 pointerScreenPos;
    // 걸어가는 중인 상호작용 대상 (없으면 null)
    private IInteractable approachTarget;

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
        // 거리 밖이면 대상으로 걸어간 뒤 도착하면 상호작용 (다른 대상을 누르면 목표를 바꿈)
        if (TryGetHoveredInteractable(pointerScreenPos, out IInteractable interactable))
        {
            if (interactable.CanInteract(transform.position))
            {
                CancelApproach();
                interactable.Interact(inventory, hotbar);
            }
            else
            {
                approachTarget = interactable;
            }
            return;
        }

        // 다른 곳을 누르면 접근을 그만두고 평소처럼 아이템 사용
        CancelApproach();

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

    // 접근 중이면 대상 쪽으로 이동시키고, 거리 안에 들어오면 상호작용
    // 직접 이동, 공격·회피·넉백·사망(Normal이 아닌 상태), 대상 파괴 시 접근 취소 (막혀도 포기하지 않음)
    private void Update()
    {
        if (approachTarget == null) return;

        // 파괴된 설치물은 Unity 기준 null이므로 Component로 확인
        Component target = approachTarget as Component;
        if (target == null || !playerState.CanAct || playerMovement.HasMoveInput)
        {
            CancelApproach();
            return;
        }

        if (approachTarget.CanInteract(transform.position))
        {
            IInteractable arrived = approachTarget;
            CancelApproach();
            arrived.Interact(inventory, hotbar);
            return;
        }

        playerMovement.SetAutoMove(target.transform.position - transform.position);
    }

    private void CancelApproach()
    {
        if (approachTarget == null) return;

        approachTarget = null;
        playerMovement.ClearAutoMove();
    }

    // 화면 위치의 광선이 처음 닿은 상호작용 대상 (거리 무관, 마우스 프리뷰도 같은 판정을 씀)
    public bool TryGetHoveredInteractable(Vector2 screenPos, out IInteractable interactable)
    {
        interactable = null;
        if (Camera.main == null) return false;

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, interactRayDistance, interactLayers, QueryTriggerInteraction.Ignore)) return false;

        interactable = hit.collider.GetComponentInParent<IInteractable>();
        return interactable != null;
    }
}
