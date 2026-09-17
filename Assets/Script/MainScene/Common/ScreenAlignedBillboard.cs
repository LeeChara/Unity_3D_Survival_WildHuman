using UnityEngine;

public class ScreenAlignedBillboard : MonoBehaviour
{
    private Transform mainCamera;

    // 투영면 정렬 빌보드
    // 몬스터 체력바에서 사용 중
    private void Awake()
    {
        mainCamera = Camera.main.transform;
    }

    private void LateUpdate()
    {
        transform.rotation = mainCamera.rotation;
    }
}
