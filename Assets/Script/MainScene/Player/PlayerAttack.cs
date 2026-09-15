using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private GameObject hitboxObject;
    [SerializeField] private HitboxController hitboxController; // hitboxObject와 같은 오브젝트

    [SerializeField] private PlayerState playerState;
    [SerializeField] private Animator animator;

    public float windupDuration = 0.2f;
    public float attackDuration = 0.5f;
    public float recoverDuration = 0.1f;
    public float attackCooldown = 1.5f;

    private enum AttackPhase { None, Windup, Attack, Recover }
    private AttackPhase attackPhase = AttackPhase.None;
    private float phaseTime;
    private float lastAttackTime = -999f; // 시작 시 바로 사용 가능하도록 충분히 작은 값
    private void Awake()
    {
        hitboxObject.SetActive(false);
    }

    private void Update()
    {
        // Attack일 때만 실행 (다른 클래스와 배타적)
        if (playerState.CurrentState != PlayerActionState.Attack) return;

        phaseTime += Time.deltaTime;

        switch (attackPhase)
        {
            case AttackPhase.Windup:
                if (phaseTime >= windupDuration)
                {
                    attackPhase = AttackPhase.Attack;
                    phaseTime = 0f;

                    hitboxController.ResetHitTargets();
                    hitboxObject.SetActive(true);
                }
                break;

            case AttackPhase.Attack:
                if (phaseTime >= attackDuration)
                {
                    attackPhase = AttackPhase.Recover;
                    phaseTime = 0f;

                    hitboxObject.SetActive(false);
                }    
                break;

            case AttackPhase.Recover:
                if (phaseTime >= recoverDuration)
                {
                    attackPhase = AttackPhase.None;
                    playerState.SetState(PlayerActionState.Normal);
                }
                break;
        }
    }

    void OnAttack(InputValue value)
    {
        // 회피 또는 공격 중에는 입력 무시
        if (!playerState.CanAct) return;
        // 쿨다운 중에는 입력 무시
        if (Time.time < lastAttackTime + attackCooldown) return;

        attackPhase = AttackPhase.Windup;
        phaseTime = 0f;
        lastAttackTime = Time.time;

        playerState.SetState(PlayerActionState.Attack);
        animator.SetTrigger("Attack");
    }
}
