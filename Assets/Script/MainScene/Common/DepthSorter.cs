using UnityEngine;
using UnityEngine.Rendering;

// 자신의 위치로 SortingGroup의 sortingOrder를 계산 (Y-Sorting)
// 파츠별 Order는 그룹 내부에서만 쓰이므로 건드리지 않음
// 피벗이 발밑 기준이라 transform.position을 그대로 사용
[RequireComponent(typeof(SortingGroup))]
public class DepthSorter : MonoBehaviour
{
    // true: 배치 시점과 기준점 변경 시에만 계산 (Prop 등)
    // false: 매 프레임 계산 (플레이어, 몬스터, 투사체 등)
    [SerializeField] private bool isStatic;

    private SortingGroup sortingGroup;

    private void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
    }

    private void OnEnable()
    {
        if (DepthSortManager.Instance == null)
        {
            Debug.LogWarning($"[DepthSorter] 씬에 DepthSortManager가 없음: {name}", this);
            return;
        }

        if (isStatic) DepthSortManager.Instance.OriginChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (isStatic && DepthSortManager.Instance != null)
            DepthSortManager.Instance.OriginChanged -= Refresh;
    }

    private void LateUpdate()
    {
        if (!isStatic) Refresh();
    }

    // 정지 오브젝트는 위치를 옮긴 뒤 직접 호출해야 함 (풀에서 꺼낸 Prop 등)
    public void Refresh()
    {
        if (DepthSortManager.Instance == null) return;
        sortingGroup.sortingOrder = DepthSortManager.Instance.GetSortingOrder(transform.position);
    }
}
