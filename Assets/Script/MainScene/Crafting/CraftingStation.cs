using System.Collections.Generic;
using UnityEngine;

// 근처에서 특정 레시피를 제작할 수 있게 해 주는 설치물 (예: 모닥불 → 익힌 고기)
// 활성화된 스테이션을 정적 목록에 등록해 두므로, 플레이어는 거리 계산만으로 찾음 (레이어·콜라이더 불필요)
public class CraftingStation : MonoBehaviour
{
    private static readonly List<CraftingStation> active = new();
    public static IReadOnlyList<CraftingStation> Active => active;

    [Tooltip("레시피의 requiredStation과 비교할 아이템 (보통 이 설치물을 설치하는 아이템)")]
    [SerializeField] private ItemData stationItem;
    [Tooltip("이 거리(수평) 안에 있으면 제작 가능")]
    [SerializeField, Min(0f)] private float range = 3f;

    public ItemData StationItem => stationItem;

    private void OnEnable()
    {
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    public bool IsInRange(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= range * range;
    }
}
