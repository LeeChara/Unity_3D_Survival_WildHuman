using UnityEngine;

// 맵 경계(월드 보더)에 투명벽을 세움. 경계 밖 청크는 보이지만 들어갈 수 없음
// 벽은 이 오브젝트의 레이어를 따르고, 물리 설정에서 그 레이어가 플레이어·몬스터와만 충돌하도록 해 둠
public class WorldBorder : MonoBehaviour
{
    [SerializeField] private WorldState worldState;
    [SerializeField] private float height = 20f;
    [SerializeField] private float thickness = 2f;

    private void Start()
    {
        Rect bounds = worldState.Bounds;
        float centerX = bounds.center.x;
        float centerZ = bounds.center.y;
        // 모서리 틈이 생기지 않도록 길이를 두께만큼 늘림
        float lengthX = bounds.width + thickness * 2f;
        float lengthZ = bounds.height + thickness * 2f;
        float halfThickness = thickness * 0.5f;

        // 안쪽 면이 정확히 경계선에 오도록 두께 절반만큼 바깥에 배치
        CreateWall("West", new Vector3(bounds.xMin - halfThickness, 0f, centerZ), new Vector3(thickness, height, lengthZ));
        CreateWall("East", new Vector3(bounds.xMax + halfThickness, 0f, centerZ), new Vector3(thickness, height, lengthZ));
        CreateWall("South", new Vector3(centerX, 0f, bounds.yMin - halfThickness), new Vector3(lengthX, height, thickness));
        CreateWall("North", new Vector3(centerX, 0f, bounds.yMax + halfThickness), new Vector3(lengthX, height, thickness));
    }

    private void CreateWall(string wallName, Vector3 position, Vector3 size)
    {
        var wall = new GameObject(wallName) { layer = gameObject.layer };
        wall.transform.SetParent(transform, false);
        // 바닥(y=0)부터 위로 세움
        wall.transform.position = position + Vector3.up * (size.y * 0.5f);

        var box = wall.AddComponent<BoxCollider>();
        box.size = size;
    }
}
