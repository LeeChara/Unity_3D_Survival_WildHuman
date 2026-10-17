using UnityEngine;

// 걸은 거리마다 발자국을 남기는 생물 (플레이어, 몬스터)
// 실제로 움직인 거리로 재므로 걷기·달리기·넉백·미끄러짐 모두 같은 방식 (빙판 위는 FootprintSystem이 막음)
// 발자국은 왼발·오른발을 번갈아 진행 방향 양옆으로 찍음 (sideOffset이 0이면 가운데 한 줄, 예: 뱀이 기어간 자국)
public class Footprinter : MonoBehaviour
{
    [SerializeField] private Sprite sprite;
    [Tooltip("발자국 크기 배율 (그림 1픽셀 = 0.1m 기준)")]
    [SerializeField, Min(0.01f)] private float scale = 1f;
    [Tooltip("발자국 사이 거리 (m)")]
    [SerializeField, Min(0.05f)] private float stride = 0.8f;
    [Tooltip("진행 방향 양옆으로 벌리는 거리 (m)")]
    [SerializeField, Min(0f)] private float sideOffset = 0.15f;
    [Tooltip("왼발 그림을 좌우 반전해서 오른발로 씀")]
    [SerializeField] private bool mirrorAlternate = true;

    private Vector3 lastPosition;
    private float travelled;
    private bool leftFoot;
    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        travelled = 0f;
    }

    private void Update()
    {
        Vector3 position = transform.position;
        Vector3 delta = position - lastPosition;
        delta.y = 0f;
        lastPosition = position;

        float distance = delta.magnitude;
        // 순간이동(세이브 복원, 풀 재사용)은 걸음으로 치지 않음
        if (distance <= 0.0001f || distance > stride * 4f) return;
        if (health != null && health.IsDead) return;

        travelled += distance;
        if (travelled < stride) return;
        travelled -= stride;

        FootprintSystem system = FootprintSystem.Instance;
        if (system == null) return;

        Vector3 forward = delta / distance;
        Vector3 right = new Vector3(forward.z, 0f, -forward.x);
        leftFoot = !leftFoot;
        Vector3 at = position + right * (leftFoot ? -sideOffset : sideOffset);
        float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        system.Stamp(at, yaw, sprite, scale, mirrorAlternate && !leftFoot);
    }
}
