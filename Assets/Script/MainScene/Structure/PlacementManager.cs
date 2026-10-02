using System.Collections.Generic;
using UnityEngine;

// 설치물 배치 판정과 생성, 설치된 목록 관리
// 설치물은 청크와 상관없이 유지 (청크 언로드 시에도 제거하지 않음)
public class PlacementManager : MonoBehaviour
{
    [Tooltip("이 레이어의 물체가 설치물 콜라이더 범위에 있으면 설치 불가 (자원, 설치물, 몬스터, 플레이어)")]
    [SerializeField] private LayerMask blockingLayers;
    [Tooltip("alignToPlayer 설치물의 회전 단위 (0이면 자유 각도)")]
    [SerializeField, Min(0f)] private float rotationStep = 45f;
    // 카메라 각도는 현재 프로젝트에선 45도로 고정되어 있음 (AxisAlignedBillboard와 동일 전제)
    [SerializeField] private float cameraYAngle = 45f;

    public static PlacementManager Instance { get; private set; }

    private readonly List<StructureHealth> placed = new();

    // 빌보드 스프라이트(미리보기 등)가 카메라를 향하는 회전
    public Quaternion BillboardRotation => Quaternion.Euler(0f, cameraYAngle, 0f);

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 설치물 루트의 회전
    // alignToPlayer면 로컬 Z축이 플레이어 → 설치 위치를 향하도록 돌려서 로컬 X축(울타리가 뻗는 방향)이 그에 수직이 됨
    public Quaternion GetRotation(PlaceableItemData item, Vector3 userPosition, Vector3 position)
    {
        if (!item.alignToPlayer) return Quaternion.identity;

        Vector3 dir = position - userPosition;
        dir.y = 0f;
        // 플레이어 바로 위면 방향을 알 수 없으므로 카메라 정면 기준 (화면 가로로 놓임)
        float angle = dir.sqrMagnitude < 0.0001f ? cameraYAngle : Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        if (rotationStep > 0f) angle = Mathf.Round(angle / rotationStep) * rotationStep;
        return Quaternion.Euler(0f, angle, 0f);
    }

    public bool CanPlace(PlaceableItemData item, Vector3 userPosition, Vector3 position)
    {
        GameObject prefab = item.placedPrefab;
        if (prefab == null) return false;

        Vector3 offset = position - userPosition;
        offset.y = 0f;
        if (offset.sqrMagnitude > item.placeRange * item.placeRange) return false;

        // 콜라이더가 없는 설치물은 겹침 검사 없이 설치
        BoxCollider box = prefab.GetComponentInChildren<BoxCollider>();
        if (box == null) return true;

        // 자식 콜라이더의 프리팹 루트 기준 위치·회전에 설치 회전을 더해 실제 놓일 자리를 계산
        Quaternion placeRotation = GetRotation(item, userPosition, position);
        Transform root = prefab.transform;
        Transform boxTransform = box.transform;
        Vector3 localCenter = root.InverseTransformPoint(boxTransform.TransformPoint(box.center));
        Quaternion rotation = placeRotation * (Quaternion.Inverse(root.rotation) * boxTransform.rotation);
        Vector3 center = position + placeRotation * localCenter;
        Vector3 halfExtents = Vector3.Scale(box.size, boxTransform.lossyScale) * 0.5f;
        // 히트박스·수집 범위 같은 트리거는 무시하고 실제 몸체만 검사
        return !Physics.CheckBox(center, halfExtents, rotation, blockingLayers, QueryTriggerInteraction.Ignore);
    }

    public GameObject Place(PlaceableItemData item, Vector3 userPosition, Vector3 position)
    {
        if (item.placedPrefab == null) return null;

        Quaternion rotation = GetRotation(item, userPosition, position);
        GameObject instance = Instantiate(item.placedPrefab, position, rotation, transform);

        if (instance.TryGetComponent(out StructureHealth structure))
        {
            placed.Add(structure);
            structure.Destroyed += OnStructureDestroyed;
        }
        return instance;
    }

    private void OnStructureDestroyed(StructureHealth structure)
    {
        structure.Destroyed -= OnStructureDestroyed;
        placed.Remove(structure);
    }
}
