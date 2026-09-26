using System;
using UnityEngine;

// Y-Sorting 공통 규칙 관리
// 카메라 시선 방향(수평)으로 멀수록 sortingOrder를 작게 매겨 먼저(뒤에) 그려지게 함
// DepthSorter보다 먼저 초기화되어야 하므로 실행 순서를 앞당김
[DefaultExecutionOrder(-100)]
public class DepthSortManager : MonoBehaviour
{
    // 카메라 각도는 현재 프로젝트에선 45도로 고정되어 있음 (AxisAlignedBillboard와 동일 전제)
    [SerializeField] private float cameraYAngle = 45f;

    // 10이면 0.1m 단위로 앞뒤 구분
    [SerializeField] private float precision = 10f;

    // sortingOrder는 short 범위(±32767)라 절대 좌표를 쓰면 무한 월드에서 넘침
    // 기준점을 카메라 위치 기준으로 이 간격마다 스냅해서 값 범위를 유지
    [SerializeField] private float originSnap = 1000f;

    public static DepthSortManager Instance { get; private set; }

    // 기준점이 바뀌면 정지 오브젝트들이 order를 재계산해야 함
    public event Action OriginChanged;

    private Transform mainCamera;
    private Vector3 axis;
    private Vector3 origin;

    private void Awake()
    {
        Instance = this;
        mainCamera = Camera.main.transform;

        float rad = cameraYAngle * Mathf.Deg2Rad;
        axis = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        origin = GetSnappedOrigin();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        Vector3 snapped = GetSnappedOrigin();
        if (snapped == origin) return;

        origin = snapped;
        OriginChanged?.Invoke();
    }

    public int GetSortingOrder(Vector3 position)
    {
        float depth = Vector3.Dot(position - origin, axis);
        return Mathf.Clamp(-Mathf.RoundToInt(depth * precision), short.MinValue, short.MaxValue);
    }

    private Vector3 GetSnappedOrigin()
    {
        Vector3 pos = mainCamera.position;
        return new Vector3(
            Mathf.Round(pos.x / originSnap) * originSnap,
            0f,
            Mathf.Round(pos.z / originSnap) * originSnap);
    }
}
