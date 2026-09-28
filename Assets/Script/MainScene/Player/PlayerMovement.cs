using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody rb;
    [SerializeField] private Facing playerFacing;
    [SerializeField] private PlayerState playerState;
    [SerializeField] private Animator animator;

    public float moveSpeed = 5.0f;
    public float sprintMultiplier = 1.5f;

    private Vector2 inputVec;
    private bool isSprintPressed;
    private PlayerHunger playerHunger;

    // 실제로 달리고 있는지 여부 (달리기 입력 + 이동 중 + 굶주림 아님)
    public bool IsSprinting { get; private set; }

    [SerializeField] private float cameraYAngle = 45f; // Billboard의 값과 반드시 일치해야함
    private Quaternion cameraRotation;
    private Vector3 cameraRight;
    private Vector3 cameraForwardFlat;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerHunger = GetComponent<PlayerHunger>();

        cameraRotation = Quaternion.Euler(0, cameraYAngle, 0);
        cameraRight = cameraRotation * Vector3.right;
        cameraForwardFlat = cameraRotation * Vector3.forward;
    }

    private void FixedUpdate()
    {
        // Normal일 때만 실행 (다른 클래스와 배타적)
        if (playerState.CurrentState != PlayerActionState.Normal)
        {
            IsSprinting = false;
            return;
        }

        // 입력 벡터는 Input Action에서 이미 정규화
        Vector3 moveVec = new Vector3(inputVec.x, 0, inputVec.y);

        // 굶주림 상태에서는 달리기 불가
        bool isStarving = playerHunger != null && playerHunger.IsStarving;
        IsSprinting = isSprintPressed && moveVec.sqrMagnitude > 0.01f && !isStarving;

        moveVec *= moveSpeed;
        if (IsSprinting) moveVec *= sprintMultiplier;

        // 쿼터뷰 형식에 맞게 회전
        moveVec = cameraRotation * moveVec;

        rb.linearVelocity = moveVec;

        // 움직임이 거의 없으면 업데이트하지 않음
        if (moveVec.sqrMagnitude > 0.01f)
        {
            playerState.SetLastDir(new Vector2(moveVec.x, moveVec.z).normalized);
            playerFacing.UpdateFacing(moveVec, cameraRight, cameraForwardFlat);

            animator.SetBool("IsMoving", true);
            animator.SetBool("IsSprinting", IsSprinting);
        }
        else
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsSprinting", false);
        }
    }
    void OnMove(InputValue value)
    {
        inputVec = value.Get<Vector2>();
    }
    void OnSprint(InputValue value)
    {
        isSprintPressed = value.isPressed;
    }
}
