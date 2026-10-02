using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 우클릭 시 핫바에서 선택한 아이템을 사용하고, 사용되면 1개 소모
public class PlayerItemUser : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerHotbar hotbar;
    [SerializeField] private PlayerState playerState;

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
}
