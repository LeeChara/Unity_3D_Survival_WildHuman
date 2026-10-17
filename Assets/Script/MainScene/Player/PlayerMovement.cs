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
    private WaterWader waterWader;

    // 실제로 달리고 있는지 여부 (달리기 입력 + 이동 중 + 굶주림 아님)
    public bool IsSprinting { get; private set; }

    // 이동 입력이 들어와 있는지 (공격 경직을 이동으로 끊을 때 사용)
    public bool HasMoveInput => inputVec.sqrMagnitude > 0.01f;

    // 직접 입력 대신 따라갈 월드 방향 (상호작용 대상으로 자동 접근, 직접 입력이 있으면 무시)
    private Vector3? autoMoveDir;

    public void SetAutoMove(Vector3 worldDir)
    {
        worldDir.y = 0f;
        autoMoveDir = worldDir.sqrMagnitude > 0.0001f ? worldDir.normalized : null;
    }

    // 자동 이동을 멈춤 (다음 FixedUpdate에서 입력이 없으면 정지)
    public void ClearAutoMove()
    {
        autoMoveDir = null;
    }

    [SerializeField] private float cameraYAngle = 45f; // Billboard의 값과 반드시 일치해야함
    private Quaternion cameraRotation;
    private Vector3 cameraRight;
    private Vector3 cameraForwardFlat;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerHunger = GetComponent<PlayerHunger>();
        waterWader = GetComponent<WaterWader>();
        // 구르기는 빙판에서도 그대로 (구르기가 끝난 뒤 남은 속도로 미끄러짐)
        if (TryGetComponent(out IceSlider iceSlider)) iceSlider.Bypass = () => playerState.CurrentState == PlayerActionState.Dodge;

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
            // 기절·잡힘 중에는 제자리에 멈춤 (몸체 마찰이 0이라 넉백 후 남은 속도로 미끄러지지 않도록)
            if (playerState.CurrentState == PlayerActionState.Stunned || playerState.CurrentState == PlayerActionState.Grabbed)
            {
                if (!rb.isKinematic)
                {
                    Vector3 velocity = rb.linearVelocity;
                    rb.linearVelocity = new Vector3(0f, velocity.y, 0f);
                }
                animator.SetBool("IsMoving", false);
                animator.SetBool("IsSprinting", false);
            }
            return;
        }

        // 입력 벡터는 Input Action에서 이미 정규화, 쿼터뷰 형식에 맞게 회전
        // 직접 입력이 없고 자동 이동 중이면(상호작용 대상으로 접근) 그 방향 (이미 월드 기준)
        Vector3 moveVec = cameraRotation * new Vector3(inputVec.x, 0, inputVec.y);
        if (!HasMoveInput && autoMoveDir.HasValue) moveVec = autoMoveDir.Value;

        // 굶주림 상태에서는 달리기 불가
        bool isStarving = playerHunger != null && playerHunger.IsStarving;
        IsSprinting = isSprintPressed && moveVec.sqrMagnitude > 0.01f && !isStarving;

        moveVec *= moveSpeed;
        // 물 위에서는 느려짐
        if (waterWader != null) moveVec *= waterWader.SpeedMultiplier;
        if (IsSprinting) moveVec *= sprintMultiplier;

        rb.linearVelocity = moveVec;

        // 움직임이 거의 없으면 업데이트하지 않음
        if (moveVec.sqrMagnitude > 0.01f)
        {
            ApplyDirection(moveVec);

            animator.SetBool("IsMoving", true);
            animator.SetBool("IsSprinting", IsSprinting);
        }
        else
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsSprinting", false);
        }
    }

    // 이동 로직이 멈춘 상태(공격 중 회피 등)에서 현재 이동 입력으로 방향·시선만 갱신 (입력이 없으면 그대로)
    public void RefreshDirectionFromInput()
    {
        if (!HasMoveInput) return;
        ApplyDirection(cameraRotation * new Vector3(inputVec.x, 0, inputVec.y));
    }

    private void ApplyDirection(Vector3 worldDir)
    {
        playerState.SetLastDir(new Vector2(worldDir.x, worldDir.z).normalized);
        playerFacing.UpdateFacing(worldDir, cameraRight, cameraForwardFlat);
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
