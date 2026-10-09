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
    [Tooltip("행동 상태가 바뀔 때 애니메이션이 섞이는 시간(초)")]
    [SerializeField] private float animationFade = 0.1f;
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

    // State 순서대로 애니메이터 상태 해시
    private int[] animationHashes;

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

        // 생성 직후 세이브의 체력으로 덮어쓸 수 있도록 Start가 아닌 Awake에서 초기화
        // (Health.Awake보다 먼저 실행돼도 Health.Awake가 바뀐 최대 체력으로 다시 채우므로 결과 동일)
        health.Init(data.maxHealth);

        CacheAnimationHashes();
    }

    protected virtual void Start()
    {
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
            PlayAnimation(State.Chase);
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

            PlayAnimation(State.Idle);
            return;
        }

        ChaseBehavior();

        if (targetDistance <= data.windupRange)
        {
            state = State.Windup;

            stateTime = 0f;

            PlayAnimation(State.Windup);
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

            PlayAnimation(State.Idle);

            return;
        }

        WindupBehavior();

        if (stateTime > data.windupDuration)
        {
            state = State.Attack;
            stateTime = 0f;

            PlayAnimation(State.Attack);

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

            PlayAnimation(State.Recover);

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

            PlayAnimation(State.Idle);
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

        PlayAnimation(State.Idle);
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

    // 행동 상태와 같은 애니메이터 상태로 바로 전환
    // 트리거 연쇄를 거치지 않으므로 상태가 연달아 바뀌거나 넉백으로 끊겨도 모션이 어긋나지 않음
    protected void PlayAnimation(State animState)
    {
        animator.CrossFadeInFixedTime(animationHashes[(int)animState], animationFade);
    }

    // 애니메이터 상태 이름은 '컨트롤러 이름 + 행동 상태' (예: ShieldboarWindup), 없으면 행동 상태 이름 그대로
    private void CacheAnimationHashes()
    {
        string prefix = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "";
        State[] states = (State[])Enum.GetValues(typeof(State));
        animationHashes = new int[states.Length];

        foreach (State s in states)
        {
            int hash = Animator.StringToHash(prefix + s);
            if (!animator.HasState(0, hash))
            {
                hash = Animator.StringToHash(s.ToString());
                if (!animator.HasState(0, hash)) Debug.LogWarning($"[MonsterAI] 애니메이터에 {prefix + s} 상태가 없음: {name}", this);
            }
            animationHashes[(int)s] = hash;
        }
    }
}
