using UnityEngine;

// 우클릭한 위치에 설치물을 세우는 아이템
// 설치 가능 판정은 미리보기와 같은 기준을 쓰도록 PlacementManager에 모아 둠
// 겹침 판정은 설치될 프리팹의 BoxCollider(자식 Collider 오브젝트)로 검사하므로 콜라이더만 맞추면 됨
[CreateAssetMenu(fileName = "PlaceableItemData", menuName = "Item/PlaceableItemData")]
public class PlaceableItemData : ItemData
{
    [Tooltip("설치될 오브젝트 (루트 회전은 0으로 둘 것)")]
    public GameObject placedPrefab;
    [Tooltip("켜면 플레이어 → 설치 위치 방향에 수직으로 회전해서 놓임 (프리팹의 로컬 X축이 가로로 뻗는 방향)")]
    public bool alignToPlayer;
    [Tooltip("플레이어로부터 설치 가능한 최대 거리")]
    [Min(0f)] public float placeRange = 3f;

    public override bool TryUse(ItemUseContext context)
    {
        PlacementManager manager = PlacementManager.Instance;
        if (manager == null) return false;

        Vector3 userPosition = context.user.transform.position;
        if (!manager.CanPlace(this, userPosition, context.aimPoint)) return false;

        manager.Place(this, userPosition, context.aimPoint);
        return true;
    }
}
