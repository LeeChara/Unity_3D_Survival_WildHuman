using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody rb;

    public float moveSpeed = 5.0f;
    public float sprintMultiplier = 1.5f;
    public float dodgeDistance = 5f;
    public float dodgeDuration = 0.5f;

    private Vector2 inputVec;
    private bool isSprinting;
    private bool isDodging;
    private float dodgeTime;
    private float dodgeSpeed;
    private Vector3 dodgeVec;
    private Vector2 lastDir; // 정지했을 때도 보고 있는 방향으로 구르도록 방향 저장

    private float cameraYAngle = 45f;
    private Quaternion cameraRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cameraRotation = Quaternion.Euler(0, cameraYAngle, 0);

        dodgeSpeed = dodgeDistance / dodgeDuration;
    }

    private void FixedUpdate()
    {
        if (isDodging)
        {
            dodgeTime += Time.fixedDeltaTime;
            rb.linearVelocity = dodgeVec;

            if (dodgeTime >= dodgeDuration)
            {
                isDodging = false;
            }
            return; // 구르기 중에는 일반 이동 무시
        }

        // 입력 벡터는 Input Action에서 이미 정규화
        Vector3 moveVec = new Vector3(inputVec.x, 0, inputVec.y);

        moveVec *= moveSpeed;
        if (isSprinting) moveVec *= sprintMultiplier;

        // 쿼터뷰 형식에 맞게 회전
        moveVec = cameraRotation * moveVec;

        rb.linearVelocity = moveVec;

        if (moveVec.sqrMagnitude > 0.01f)
        {
            lastDir = new Vector2(moveVec.x, moveVec.z).normalized;
            transform.rotation = Quaternion.LookRotation(moveVec);
        }
    }
    void OnMove(InputValue value)
    {
        inputVec = value.Get<Vector2>();
    }
    void OnSprint(InputValue value)
    {
        isSprinting = value.isPressed;
    }
    void OnDodge(InputValue value)
    {
        if (isDodging) return;

        dodgeVec = new Vector3(lastDir.x, 0, lastDir.y) * dodgeSpeed;

        isDodging = true;
        dodgeTime = 0f;
    }
}
