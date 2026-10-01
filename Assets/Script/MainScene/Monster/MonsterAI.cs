using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class MonsterAI : MonoBehaviour
{
    [SerializeField] protected GameObject hitbox;
    [SerializeField] protected HitboxController hitboxController;
    [SerializeField] protected LayerMask playerLayer;
    [SerializeField] protected LayerMask monsterLayer;
    [SerializeField] protected MonsterData data;
    [SerializeField] protected Animator animator;
    [SerializeField] protected Facing monsterFacing;
    [SerializeField] private float cameraYAngle = 45f; // Billboard의 값과 반드시 일치해야함
    public MonsterData Data => data;
    protected enum State { Idle, Chase, Windup, Attack, Recover }

    protected State state = State.Idle;
    protected float stateTime;

    // 플레이어 인식/인식 해제 시점을 이펙트 등 외부 시스템에 알림
    public event Action PlayerDetected;
    public event Action PlayerLost;

    protected Rigidbody rb;
    protected Health health;
    protected Knockback knockback;
    protected Transform target;
    private Health targetHealth;
    private bool isTargetPlayer;

    protected Vector3 targetDirection;
    protected float targetDistance;

    protected Vector3 attackDirection;

    private Vector3? wanderTarget; // Nullable
    private float wanderCooldownTime;

    protected Vector3 cameraRight;
    protected Vector3 cameraForwardFlat;
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
        health.Died += OnDied;
        hitbox.SetActive(false);

        knockback = GetComponent<Knockback>();
        if (knockback != null)
        {
            knockback.IsImmune = IsSuperArmor;
            // 플레이 중 데이터 에셋 수정도 즉시 반영되도록 피격 시점에 참조
            knockback.ResistanceProvider = () => data.knockbackResistance;
            knockback.Started += OnKnockbackStarted;
        }

        Quaternion cameraRotation = Quaternion.Euler(0, cameraYAngle, 0);
        cameraRight = cameraRotation * Vector3.right;
        cameraForwardFlat = cameraRotation * Vector3.forward;
    }

    protected virtual void Start()
    {
        health.Init(data.maxHealth);
        if (TryGetComponent(out ItemDropOnDeath dropOnDeath)) dropOnDeath.SetDropTable(data.dropTable);
    }

    protected virtual void OnDestroy()
    {
        if (health != null) health.Died -= OnDied;
    }

    // 몬스터의 행동 기반은 목표물과의 거리
    // 따라서 목표물 갱신을 먼저 진행
    protected virtual void Update()
    {
        UpdateTarget();

        // 넉백 중에는 Knockback이 속도를 제어하므로 상태머신 정지
        if (knockback != null && knockback.IsActive) return;

        stateTime += Time.deltaTime;

        if (target != null)
        {
            targetDistance = Vector3.Distance(transform.position, target.position);
            targetDirection = target.position - transform.position;
            targetDirection.y = 0f;
            targetDirection.Normalize();
        }

        switch (state)
        {
            case State.Idle:
                Idle();
                break;
            case State.Chase:
                Chase();
                break;
            case State.Windup:
                Windup();
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
        // 목표물이 Destroy된 경우에도 인식 해제 처리를 거치도록 함
        if (target == null && !ReferenceEquals(target, null))
        {
            ClearTarget();
        }

        // 추격 중인 목표물이 사망하면 시체가 남아 있어도 추격 포기
        if (target != null && targetHealth != null && targetHealth.IsDead)
        {
            ClearTarget();
        }

        // 추격 중인 목표물이 추격 범위보다 멀어지면 추격 포기
        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > data.chaseGiveUpRange)
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
            // 시체는 피격 판정을 위해 콜라이더가 남아 있으므로 탐지 단계에서 제외
            if (c.TryGetComponent(out Health otherHealth) && otherHealth.IsDead) continue;

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
    private void SetTarget(Transform detectedTarget)
    {
        target = detectedTarget;
        targetHealth = target.GetComponent<Health>();
        isTargetPlayer = (playerLayer.value & (1 << target.gameObject.layer)) != 0;
        if (isTargetPlayer) PlayerDetected?.Invoke();
    }

    private void ClearTarget()
    {
        this.target = null;
        targetHealth = null;
        // Destroy된 목표물은 레이어를 읽을 수 없으므로 SetTarget 시점에 저장한 값으로 판단
        if (isTargetPlayer)
        {
            isTargetPlayer = false;
            PlayerLost?.Invoke();
        }
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

    protected virtual void Chase()
    {
        if (target == null)
        {
            state = State.Idle;

            ResetTrigger();
            animator.SetTrigger("Reset");
            return;
        }

        ChaseBehavior();

        if (targetDistance <= data.windupRange)
        {
            state = State.Windup;

            stateTime = 0f;

            animator.SetTrigger("Windup");
        }
    }

    protected virtual void ChaseBehavior()
    {
        rb.linearVelocity = targetDirection * data.chaseSpeed;
        transform.rotation = Quaternion.LookRotation(targetDirection);
    }

    protected virtual void Windup()
    {
        if (target == null)
        {
            state = State.Idle;

            ResetTrigger();
            animator.SetTrigger("Reset");

            return;
        }

        WindupBehavior();

        if (stateTime > data.windupDuration)
        {
            state = State.Attack;
            stateTime = 0f;

            animator.SetTrigger("Attack");

            attackDirection = targetDirection;
            // 공격 시작 시점에 해당 공격의 수치를 적용 (공격 종류별 수치, 플레이 중 수정 반영)
            hitboxController.SetAttack(data.attack);
            hitboxController.ResetHitTargets();
            hitbox.SetActive(true);
        }
    }

    protected virtual void WindupBehavior()
    {
        rb.linearVelocity = Vector3.zero;
        transform.rotation = Quaternion.LookRotation(targetDirection);
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

    private bool IsSuperArmor()
    {
        // 시체는 공격 상태로 멈춰 있어도 넉백되도록 슈퍼아머 해제
        if (health.IsDead) return false;
        return data.superArmorWhileAttacking && (state == State.Windup || state == State.Attack);
    }

    // 공격 준비/공격 중 넉백되면 공격을 취소하고 Idle부터 다시 판단
    protected virtual void OnKnockbackStarted()
    {
        if (health.IsDead) return;
        if (state != State.Windup && state != State.Attack) return;

        hitbox.SetActive(false);
        state = State.Idle;
        stateTime = 0f;

        ResetTrigger();
        animator.SetTrigger("Reset");
    }

    // 사망 시 모든 행동을 멈추고 마지막 포즈로 정지
    // 시체 유지, 제거는 MonsterHealth가 담당
    protected virtual void OnDied()
    {
        hitbox.SetActive(false);

        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(0f, velocity.y, 0f);

        animator.speed = 0f;
        enabled = false;
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
