using UnityEngine;

// 커서로 가리키고 우클릭하면 상호작용하는 설치물 (예: 모닥불에 아이템 올리기·회수)
// PlayerItemUser가 들고 있는 아이템을 사용하기 전에 먼저 확인함
public interface IInteractable
{
    // from(플레이어 위치)에서 상호작용할 수 있는 거리인지
    bool CanInteract(Vector3 from);
    void Interact(Inventory inventory, PlayerHotbar hotbar);
}
