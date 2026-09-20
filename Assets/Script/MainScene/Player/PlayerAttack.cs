using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private GameObject hitboxObject;
    [SerializeField] private HitboxController hitboxController; // hitboxObject와 같은 오브젝트

    [SerializeField] private PlayerState playerState;
    [SerializeField] private Animator animator;
    [SerializeField] private Facing playerFacing;
    [SerializeField] private Transform attackPivot; // Hitbox(및 추후 검기 이펙트)를 클릭 방향으로 회전시키는 피벗

    public float windupDuration = 0.2f;
    public float attackDuration = 0.5f;
    public float recoverDuration = 0.1f;
    public float attackCooldown = 1.5f;

    [SerializeField] private float cameraYAngle = 45f; // PlayerMovement/Billboard의 값과 반드시 일치해야함
    private Vector3 cameraRight;
    private Vector3 cameraForwardFlat;

    private Vector2 pointerScreenPos;

    private enum AttackPhase { None, Windup, Attack, Recover }
    private AttackPhase attackPhase = AttackPhase.None;
    private float phaseTime;
    private float lastAttackTime = -999f; // 시작 시 바로 사용 가능하도록 충분히 작은 값
    private void Awake()
    {
        hitboxObject.SetActive(false);

        Quaternion cameraRotation = Quaternion.Euler(0, cameraYAngle, 0);
        cameraRight = cameraRotation * Vector3.right;
        cameraForwardFlat = cameraRotation * Vector3.forward;
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

    void OnPoint(InputValue value)
    {
        pointerScreenPos = value.Get<Vector2>();
    }

    void OnAttack(InputValue value)
    {
        // 회피 또는 공격 중에는 입력 무시
        if (!playerState.CanAct) return;
        // 쿨다운 중에는 입력 무시
        if (Time.time < lastAttackTime + attackCooldown) return;

        Vector3 attackDir = ComputeAttackDirection();
        attackPivot.rotation = Quaternion.LookRotation(attackDir);
        playerFacing.UpdateFacing(attackDir, cameraRight, cameraForwardFlat);

        attackPhase = AttackPhase.Windup;
        phaseTime = 0f;
        lastAttackTime = Time.time;

        playerState.SetState(PlayerActionState.Attack);
        animator.SetTrigger("Attack");
    }

    private Vector3 ComputeAttackDirection()
    {
        Ray ray = Camera.main.ScreenPointToRay(pointerScreenPos);
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            Vector3 dir = hitPoint - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
        }

        return cameraForwardFlat; // 예외적으로 교차하지 않을 때의 대비값
    }
}
