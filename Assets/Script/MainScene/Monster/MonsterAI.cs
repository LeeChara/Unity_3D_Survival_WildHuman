using UnityEngine;

public class MonsterAI : MonoBehaviour
{
    [SerializeField] protected GameObject hitbox;
    [SerializeField] protected HitboxController hitboxController;
    [SerializeField] protected LayerMask playerLayer;
    [SerializeField] protected LayerMask monsterLayer;
    [SerializeField] protected MonsterData data;
    [SerializeField] protected Animator animator;
    [SerializeField] private Facing monsterFacing;
    [SerializeField] private float cameraYAngle = 45f; // Billboard의 값과 반드시 일치해야함
    public MonsterData Data => data;
    protected enum State { Idle, Chase, Windup, Attack, Recover }

    private State state = State.Idle;
    private float stateTime;

    protected Rigidbody rb;
    protected Health health;
    protected Transform target;

    protected Vector3 attackDirection;

    private Vector3? wanderTarget; // Nullable
    private float wanderCooldownTime;

    protected Vector3 cameraRight;
    protected Vector3 cameraForwardFlat;
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
        hitbox.SetActive(false);

        Quaternion cameraRotation = Quaternion.Euler(0, cameraYAngle, 0);
        cameraRight = cameraRotation * Vector3.right;
        cameraForwardFlat = cameraRotation * Vector3.forward;
    }

    protected virtual void Start()
    {
        health.Init(data.maxHealth);
    }

    // 몬스터의 행동 기반은 목표물과의 거리
    // 따라서 목표물 갱신을 먼저 진행
    protected virtual void Update()
    {
        UpdateTarget();

        stateTime += Time.deltaTime;

        float distance = 0f;
        Vector3 direction = Vector3.zero;
        if (target != null)
        {
            distance = Vector3.Distance(transform.position, target.position);
            direction = target.position - transform.position;
            direction.y = 0f;
            direction.Normalize();
        }

        switch (state)
        {
            case State.Idle:
                Idle();
                break;
            case State.Chase:
                Chase(distance, direction);
                break;
            case State.Windup:
                Windup(direction);
                break;
            case State.Attack:
                Attack();
                break;
            case State.Recover:
                Recover();
                break;
        }
        
        // 실제로 이동 중인 방향이 있을 때만 Facing 갱신
        Vector3 currentVelocity = rb.linearVelocity;
        if (currentVelocity.sqrMagnitude > 0.01f)
        {
            monsterFacing.UpdateFacing(currentVelocity, cameraRight, cameraForwardFlat);
        }

        bool isWandering = state == State.Idle && currentVelocity.sqrMagnitude > 0.01f;
        animator.SetBool("IsWandering", isWandering);
    }

    protected virtual void UpdateTarget()
    {
        // 추격 중인 목표물이 추격 범위보다 멀어지면 추격 포기
        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > data.chaseRange)
            {
                ClearTarget();
            }
            return;
        }

        Transform detectedTarget = FindPlayerInRange();
        if (detectedTarget != null)
        {
            SetTarget(detectedTarget);
            return;
        }
        detectedTarget = FindHostileTargetInRange();
        if (detectedTarget != null)
        {
            SetTarget(detectedTarget);
        }
    }
    private Transform FindPlayerInRange()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, data.chaseRange, playerLayer);
        return hits.Length > 0 ? hits[0].transform : null;
    }
    private Transform FindHostileTargetInRange()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, data.chaseRange, monsterLayer);
        Transform closest = null;
        float closestDistance = float.MaxValue;

        foreach (Collider c in hits)
        {
            if (c.transform == transform) continue;

            MonsterAI otherAI = c.GetComponent<MonsterAI>();
            if (otherAI == null || !IsInHostileTargets(otherAI.Data)) continue;

            float distance = Vector3.Distance(transform.position, c.transform.position);
            if (distance < closestDistance)
            {
                closest = c.transform;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private bool IsInHostileTargets(MonsterData otherData)
    {
        foreach (MonsterData hostileTarget in data.hostileTargets)
        {
            if (hostileTarget == otherData) return true;
        }
        return false;
    }
    private void SetTarget(Transform dectectedTarget)
    {
        target = dectectedTarget;   
    }

    private void ClearTarget()
    {
        this.target = null;
    }
    protected virtual void Idle()
    {
        // 목표가 감지되면 Chase 상태로 전환
        if (target != null)
        {
            state = State.Chase;
            ResetTrigger();
            animator.SetTrigger("Chase");
            return;
        }

        Wander();
    }

    protected virtual void Wander()
    {
        if (wanderCooldownTime > 0f)
        {
            wanderCooldownTime -= Time.deltaTime;
            rb.linearVelocity = Vector3.zero;
            return;
        }

        if (wanderTarget == null)
        {
            Vector3 randomOffset = Random.insideUnitSphere * data.wanderRange;
            randomOffset.y = 0;
            wanderTarget = transform.position + randomOffset;
        }

        Vector3 wanderDistance = wanderTarget.Value - transform.position;
        wanderDistance.y = 0;

        if (wanderDistance.magnitude < 0.5f)
        {
            wanderTarget = null;
            rb.linearVelocity = Vector3.zero;
            wanderCooldownTime = Mathf.Max(0f, data.wanderCooldown + Random.Range(-0.5f, 0.5f));
            return;
        }

        Vector3 wanderDirection = wanderDistance.normalized;
        rb.linearVelocity = wanderDirection * data.moveSpeed;
        transform.rotation = Quaternion.LookRotation(wanderDirection);
    }

    protected virtual void Chase(float distance, Vector3 direction)
    {
        if (target == null)
        {
            state = State.Idle;

            ResetTrigger();
            animator.SetTrigger("Reset");
            return;
        }

        ChaseBehavior(distance, direction);

        if (distance <= data.windupRange)
        {
            state = State.Windup;

            stateTime = 0f;

            animator.SetTrigger("Windup");
        }
    }

    protected virtual void ChaseBehavior(float distance, Vector3 direction)
    {
        rb.linearVelocity = direction * data.chaseSpeed;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    protected virtual void Windup(Vector3 direction)
    {
        if (target == null)
        {
            state = State.Idle;

            ResetTrigger();
            animator.SetTrigger("Reset");

            return;
        }

        WindupBehavior(direction);

        if (stateTime > data.windupDuration)
        {
            state = State.Attack;
            stateTime = 0f;

            animator.SetTrigger("Attack");

            attackDirection = direction;
            hitboxController.ResetHitTargets();
            hitbox.SetActive(true);
        }
    }

    protected virtual void WindupBehavior(Vector3 direction)
    {
        rb.linearVelocity = Vector3.zero;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    protected virtual void Attack()
    {
        AttackBehavior();

        if (stateTime > data.attackDuration)
        {
            state = State.Recover;
            stateTime = 0f;

            animator.SetTrigger("Recover");

            hitbox.SetActive(false);
        }
    }

    protected virtual void AttackBehavior()
    {
        rb.linearVelocity = attackDirection * data.attackSpeed;
    }

    protected virtual void Recover()
    {
        RecoverBehavior();

        if (stateTime > data.recoverDuration)
        {
            state = State.Idle;

            ResetTrigger();
            animator.SetTrigger("Reset");
        }
    }

    protected virtual void RecoverBehavior()
    {
        rb.linearVelocity = Vector3.zero;
    }

    // 소비되지 않은 트리거 처리
    private void ResetTrigger()
    {
        animator.ResetTrigger("Chase");
        animator.ResetTrigger("Windup");
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("Recover");
        animator.ResetTrigger("Reset");
    }
}
