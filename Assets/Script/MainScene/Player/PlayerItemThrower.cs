using UnityEngine;

// 인벤토리에서 꺼낸 아이템을 마우스 커서 방향의 땅으로 던짐
public class PlayerItemThrower : MonoBehaviour
{
    public void Throw(ItemData item, int count, Vector2 pointerScreenPos)
    {
        if (item == null || count <= 0) return;

        ItemDropper.Instance.Throw(item, count, transform.position, ComputeThrowDirection(pointerScreenPos));
    }

    // 커서 아래 땅(플레이어 높이의 수평면) 지점을 향하는 수평 방향 (PlayerAttack의 공격 방향과 같은 방식)
    private Vector3 ComputeThrowDirection(Vector2 pointerScreenPos)
    {
        Camera cam = Camera.main;
        Ray ray = cam.ScreenPointToRay(pointerScreenPos);
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 dir = ray.GetPoint(distance) - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
        }

        // 예외적으로 교차하지 않을 때의 대비값: 카메라가 바라보는 수평 방향
        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        return forward.normalized;
    }
}
