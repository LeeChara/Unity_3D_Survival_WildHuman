using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDodge : MonoBehaviour
{
    private static readonly int IsDodgingHash = Animator.StringToHash("IsDodging");

    private Rigidbody rb;
    [SerializeField] private PlayerState playerState;
    [SerializeField] private Animator animator;

    public float dodgeDistance = 5f;
    public float dodgeDuration = 0.5f;
    public float dodgeCooldown = 1.5f;

    private float dodgeTime;
    private float dodgeSpeed;
    private Vector3 dodgeVec;
    private float lastDodgeTime = -999f; // 시작 시 바로 사용 가능하도록 충분히 작은 값
    private PlayerHunger playerHunger;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerHunger = GetComponent<PlayerHunger>();

        dodgeSpeed = dodgeDistance / dodgeDuration;
    }

    private void FixedUpdate()
    {
        // Dodge일 때만 실행 (다른 클래스와 배타적)
        if (playerState.CurrentState != PlayerActionState.Dodge)
        {
            // 사망 등으로 구르기가 도중에 끊긴 경우에도 모션이 남지 않게 함
            if (animator.GetBool(IsDodgingHash)) animator.SetBool(IsDodgingHash, false);
            return;
        }

        dodgeTime += Time.fixedDeltaTime;
        rb.linearVelocity = dodgeVec;

        if (dodgeTime >= dodgeDuration)
        {
            playerState.SetState(PlayerActionState.Normal);
            // 클립 길이와 상관없이 이동이 끝나는 순간 구르기 모션도 끝냄
            animator.SetBool(IsDodgingHash, false);
        }
    }
    void OnDodge(InputValue value)
    {
        // 회피 또는 공격 중에는 입력 무시
        if (!playerState.CanAct) return;
        // 쿨다운 중에는 입력 무시
        if (Time.time < lastDodgeTime + dodgeCooldown) return;
        // 굶주림 상태에서는 회피 불가
        if (playerHunger != null && playerHunger.IsStarving) return;

        Vector2 lastDir = playerState.LastDir;
        dodgeVec = new Vector3(lastDir.x, 0, lastDir.y) * dodgeSpeed;
        dodgeTime = 0f;
        lastDodgeTime = Time.time;

        playerState.SetState(PlayerActionState.Dodge);
        animator.SetBool(IsDodgingHash, true);
        animator.SetTrigger("Dodge");
    }
}
