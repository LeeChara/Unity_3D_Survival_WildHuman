using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDodge : MonoBehaviour
{
    private Rigidbody rb;
    [SerializeField] private PlayerState playerState;
    [SerializeField] private Animator animator;

    public float dodgeDistance = 5f;
    public float dodgeDuration = 0.75f;
    public float dodgeCooldown = 1.5f;

    private float dodgeTime;
    private float dodgeSpeed;
    private Vector3 dodgeVec;
    private float lastDodgeTime = -999f; // 시작 시 바로 사용 가능하도록 충분히 작은 값

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        dodgeSpeed = dodgeDistance / dodgeDuration;
    }

    private void FixedUpdate()
    {
        // Dodge일 때만 실행 (다른 클래스와 배타적)
        if (playerState.CurrentState != PlayerActionState.Dodge) return;

        dodgeTime += Time.fixedDeltaTime;
        rb.linearVelocity = dodgeVec;

        if (dodgeTime >= dodgeDuration)
        {
            playerState.SetState(PlayerActionState.Normal);
        }
    }
    void OnDodge(InputValue value)
    {
        // 회피 또는 공격 중에는 입력 무시
        if (!playerState.CanAct) return;
        // 쿨다운 중에는 입력 무시
        if (Time.time < lastDodgeTime + dodgeCooldown) return;

        Vector2 lastDir = playerState.LastDir;
        dodgeVec = new Vector3(lastDir.x, 0, lastDir.y) * dodgeSpeed;
        dodgeTime = 0f;
        lastDodgeTime = Time.time;

        playerState.SetState(PlayerActionState.Dodge);
        animator.SetTrigger("Dodge");
    }
}
