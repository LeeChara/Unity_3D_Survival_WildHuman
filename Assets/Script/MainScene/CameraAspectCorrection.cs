using UnityEngine;

// 직교 카메라가 비스듬히 내려다보면 서 있는 스프라이트가 세로로 cos(내려다보는 각도)만큼 눌려 보임
// 투영을 세로로 그 역수만큼 늘려서 스프라이트를 원본 비율(정사각형 픽셀)로 보이게 함
// 가로로 보이는 범위는 orthographicSize 그대로이고, 세로 범위만 줄어듦
// Cinemachine이 카메라를 갱신한 뒤 적용하도록 늦게 실행
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Camera))]
public class CameraAspectCorrection : MonoBehaviour
{
    [Tooltip("끄면 보정 없이 기본 직교 투영 사용")]
    [SerializeField] private bool correct = true;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void OnDisable()
    {
        if (cam != null) cam.ResetProjectionMatrix();
    }

    private void LateUpdate()
    {
        if (!correct || !cam.orthographic)
        {
            cam.ResetProjectionMatrix();
            return;
        }

        // 수평 기준 내려다보는 각도
        float pitch = Mathf.Asin(Mathf.Clamp(-cam.transform.forward.y, -1f, 1f));
        float stretch = 1f / Mathf.Max(Mathf.Cos(pitch), 0.01f);

        float halfWidth = cam.orthographicSize * cam.aspect;
        float halfHeight = cam.orthographicSize / stretch;
        cam.projectionMatrix = Matrix4x4.Ortho(-halfWidth, halfWidth, -halfHeight, halfHeight, cam.nearClipPlane, cam.farClipPlane);
    }
}
