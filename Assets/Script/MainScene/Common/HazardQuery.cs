using UnityEngine;

// 어떤 지점이 위험 지형(마그마 등 Hazard 트리거) 안인지 확인 (설치 판정, 몬스터 스폰 위치 등)
// 트리거에 IHazardArea가 있으면 실제 모양으로, 없으면 트리거 범위 전체를 위험 지형으로 봄
public static class HazardQuery
{
    private static readonly Collider[] buffer = new Collider[16];

    public static bool IsInside(Vector3 position, LayerMask layers)
    {
        if (layers == 0) return false;

        int count = Physics.OverlapSphereNonAlloc(position, 0.1f, buffer, layers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            IHazardArea area = buffer[i].GetComponentInParent<IHazardArea>();
            if (area == null || area.Contains(position)) return true;
        }
        return false;
    }
}
