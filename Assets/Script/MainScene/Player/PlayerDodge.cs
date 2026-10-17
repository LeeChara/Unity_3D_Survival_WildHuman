using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDodge : MonoBehaviour
{
    private static readonly int IsDodgingHash = Animator.StringToHash("IsDodging");

    private Rigidbody rb;
    [SerializeField] private PlayerState playerState;
    [SerializeField] private Animator animator;
    [Tooltip("회피 거리·시간·쿨다운, 공격 중 회피 허용 여부")]
    [SerializeField] private PlayerCombatSetting setting;
    [SerializeField] private PlayerInputBuffer inputBuffer;
    [SerializeField] private PlayerAttack playerAttack; // 공격 중 회피 시 공격 취소
    [SerializeField] private PlayerMovement playerMovement; // 공격 중 회피 방향을 현재 이동 입력으로 갱신

    private float dodgeTime;
    private Vector3 dodgeVec;
    private float lastDodgeTime = -999f; // 시작 시 바로 사용 가능하도록 충분히 작은 값
    private PlayerHunger playerHunger;

    // 구르기를 시작한 시점을 효과음 등 외부 시스템에 알림
    public event System.Action Dodged;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerHunger = GetComponent<PlayerHunger>();
    }

    // 선입력된 회피는 실행할 수 있게 되는 순간 실행 (쿨다운이 끝나거나 넉백이 끝난 직후 등)
    private void Update()
    {
        if (inputBuffer.Has(BufferedAction.Dodge) && CanStartDodge())
        {
            inputBuffer.Clear();
            StartDodge();
        }
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

        if (dodgeTime >= setting.dodgeDuration)
        {
            playerState.SetState(PlayerActionState.Normal);
            // 클립 길이와 상관없이 이동이 끝나는 순간 구르기 모션도 끝냄
            animator.SetBool(IsDodgingHash, false);
        }
    }

    void OnDodge(InputValue value)
    {
        // 지금 회피할 수 없으면(회피·넉백 중, 쿨다운 등) 선입력으로 기억해 두고 Update에서 실행
        if (!CanStartDodge())
        {
            inputBuffer.Push(BufferedAction.Dodge);
            return;
        }

        StartDodge();
    }

    // 평소 상태 또는 (설정에 따라) 공격 중이면 회피 가능
    private bool CanStartDodge()
    {
        if (Time.time < lastDodgeTime + setting.dodgeCooldown) return false;
        // 굶주림 상태에서는 회피 불가
        if (playerHunger != null && playerHunger.IsStarving) return false;

        if (playerState.CanAct) return true;
        return setting.dodgeCancelsAttack && playerState.CurrentState == PlayerActionState.Attack;
    }

    private void StartDodge()
    {
        if (playerState.CurrentState == PlayerActionState.Attack)
        {
            playerAttack.CancelAttack();
            // 공격 중에는 이동 로직이 멈춰 방향이 갱신되지 않으므로 지금 누른 방향으로 맞춤
            playerMovement.RefreshDirectionFromInput();
        }

        Vector2 lastDir = playerState.LastDir;
        float dodgeSpeed = setting.dodgeDistance / setting.dodgeDuration;
        dodgeVec = new Vector3(lastDir.x, 0, lastDir.y) * dodgeSpeed;
        dodgeTime = 0f;
        lastDodgeTime = Time.time;

        playerState.SetState(PlayerActionState.Dodge);
        animator.SetBool(IsDodgingHash, true);
        animator.SetTrigger("Dodge");
        Dodged?.Invoke();
    }
}
