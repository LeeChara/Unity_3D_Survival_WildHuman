using UnityEngine;

// 아이템 사용 시 아이템에 넘겨주는 정보
public readonly struct ItemUseContext
{
    // 아이템을 사용한 오브젝트 (필요한 컴포넌트는 여기서 찾아 씀)
    public readonly GameObject user;
    // 커서 아래 땅 지점 (설치 아이템 등에서 사용)
    public readonly Vector3 aimPoint;

    public ItemUseContext(GameObject user, Vector3 aimPoint)
    {
        this.user = user;
        this.aimPoint = aimPoint;
    }
}
