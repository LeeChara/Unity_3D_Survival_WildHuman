using UnityEngine;

public class PlayerBillboard : MonoBehaviour
{
    [SerializeField] private float cameraYAngle = 45f;

    // 카메라 각도는 현재 프로젝트에선 45도로 고정되어 있음
    // 따라서 Awake에서 한 번만 회전 적용
    // 이후 카메라 연출이 추가되면 스크립트 수정 필요
    private void Awake()
    {
        transform.rotation = Quaternion.Euler(0f, cameraYAngle, 0f);
    }
}
