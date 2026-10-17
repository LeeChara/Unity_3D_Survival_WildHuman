using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class MonsterAI : MonoBehaviour, IStunnable
{
    [Tooltip("공격 판정 (보스처럼 패턴이 직접 판정하면 비워둠)")]
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
    // Stunned: 음파 등으로 기절 (애니메이터에 상태가 없으면 Idle 모션 사용)
    protected enum State { Idle, Chase, Windup, Attack, Recover, Stunned }

    protected State state = State.Idle;
    protected float stateTime;

    // 플레이어 인식/인식 해제 시점을 이펙트 등 외부 시스템에 알림
    public event Action PlayerDetected;
    public event Action PlayerLost;
    public bool IsStunned => state == State.Stunned;
    // 피해를 받지 않는 상태 (MonsterHealth가 읽음, 예: 기절 중에만 맞는 문지기)
    public virtual bool IsInvulnerable => false;
    private float stunRemaining;

    protected Rigidbody rb;
    protected Health health;
    protected Knockback knockback;
    private WaterWader waterWader;
    protected Transform target;
    private Health targetHealth;
    private bool isTargetPlayer;

    protected Vector3 targetDirection;
    protected float targetDistance;

    protected Vector3 attackDirection;

    private Vector3? wanderTarget; // Nullable
    private float wanderCooldownTime;

    // 배회 방향을 플레이어 쪽으로 기울이는 대상과 기우는 정도 (MonsterSpawner가 만든 몬스터만 설정)
    private Transform driftTarget;
    private MonsterSpawner driftSource;

    protected Vector3 cameraRight;
    protected Vector3 cameraForwardFlat;

    // State 순서대로 애니메이터 상태 해시
    private int[] animationHashes;

    // 평소 이동(배회·추적·후퇴) 속도 배율 (물 위면 느려짐, 공격 돌진에는 쓰지 않음)
    protected float MoveScale => waterWader != null ? waterWader.SpeedMultiplier : 1f;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
        health.Died += OnDied;
        health.Damaged += OnDamaged;
        if (hitbox != null) hitbox.SetActive(false);

        knockback = GetComponent<Knockback>();
        waterWader = GetComponent<WaterWader>();
        // 공격 돌진은 빙판에서도 그대로 (돌진이 끝난 뒤 남은 속도로 미끄러짐)
        if (TryGetComponent(out IceSlider iceSlider)) iceSlider.Bypass = () => state == State.Attack;
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
        if (health != null)
        {
            health.Died -= OnDied;
            health.Damaged -= OnDamaged;
        }
    }

    // 사망 소리는 OnDied에서
    private void OnDamaged()
    {
        if (!health.IsDead) PlaySfx(data.HurtCue);
    }

    // 몬스터의 행동 기반은 목표물과의 거리
    // 따라서 목표물 갱신을 먼저 진행
    protected virtual void Update()
    {
        UpdateCallSfx();
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
            case State.Stunned:
                Stunned();
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

        // 추격 중인 목표물이 추격 범위보다 멀어지거나 더 쫓을 수 없는 곳으로 가면 추격 포기
        if (target != null)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > data.chaseGiveUpRange || !CanTarget(target))
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
        foreach (Collider hit in hits)
        {
            // 사망한 플레이어는 부활 전까지 시체로 남아 있으므로 탐지 단계에서 제외 (추격 포기 직후 다시 인식하지 않도록)
            if (hit.TryGetComponent(out Health playerHealth) && playerHealth.IsDead) continue;
            if (CanTarget(hit.transform)) return hit.transform;
        }
        return null;
    }

    // 이 대상을 쫓을 수 있는지 (예: 보스는 둥지 밖으로 나간 대상을 쫓지 않음)
    protected virtual bool CanTarget(Transform candidate) => true;
    private Transform FindHostileTargetInRange()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, data.chaseRange, monsterLayer);
        Transform closest = null;
        float closestDistance = float.MaxValue;

        foreach (Collider c in hits)
        {
            if (c.transform == transform) continue;

            MonsterAI otherAI = c.GetComponent<MonsterAI>();
            if (otherAI == null || !data.IsHostileTo(otherAI.Data) || !CanTarget(c.transform)) continue;
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

    private void SetTarget(Transform detectedTarget)
    {
        target = detectedTarget;
        targetHealth = target.GetComponent<Health>();
        isTargetPlayer = (playerLayer.value & (1 << target.gameObject.layer)) != 0;
        if (isTargetPlayer)
        {
            // 경계에서 인식·해제가 반복되거나 목표가 계속 바뀌어도 발견 소리는 가끔만
            if (Time.time >= nextDetectSfx)
            {
                nextDetectSfx = Time.time + DetectSfxCooldown;
                PlaySfx(data.DetectCue);
            }
            PlayerDetected?.Invoke();
        }
    }

    private const float DetectSfxCooldown = 6f;
    private float nextDetectSfx;

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
            if (driftSource != null && driftTarget != null) randomOffset = DriftTowardPlayer(randomOffset);
            wanderTarget = transform.position + randomOffset;
        }

        Vector3 wanderDistance = wanderTarget.Value - transform.position;
        wanderDistance.y = 0;

        if (wanderDistance.magnitude < 0.5f)
        {
            wanderTarget = null;
            rb.linearVelocity = Vector3.zero;
            float cooldownScale = driftSource != null ? driftSource.WanderCooldownScale : 1f;
            wanderCooldownTime = Mathf.Max(0f, (data.wanderCooldown + Random.Range(-0.5f, 0.5f)) * cooldownScale);
            return;
        }

        Vector3 wanderDirection = wanderDistance.normalized;
        rb.linearVelocity = wanderDirection * data.moveSpeed * MoveScale;
        transform.rotation = Quaternion.LookRotation(wanderDirection);
    }

    // 스포너가 관리하는 몬스터만 호출 (문지기·보스처럼 다른 경로로 만든 몬스터는 무작위 배회 유지)
    public void EnablePlayerDrift(Transform player, MonsterSpawner source)
    {
        driftTarget = player;
        driftSource = source;
    }

    // 무작위 방향을 플레이어 쪽으로 기울이고, 너무 짧게 움직이지 않도록 거리는 배회 범위의 절반 이상
    private Vector3 DriftTowardPlayer(Vector3 randomOffset)
    {
        Vector3 toPlayer = driftTarget.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.01f) return randomOffset;

        Vector3 randomDirection = randomOffset.sqrMagnitude > 0.0001f ? randomOffset.normalized : toPlayer.normalized;
        Vector3 direction = Vector3.Slerp(randomDirection, toPlayer.normalized, driftSource.PlayerDrift);
        direction.y = 0f;
        return direction.normalized * Random.Range(data.wanderRange * 0.5f, data.wanderRange);
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
        rb.linearVelocity = targetDirection * data.chaseSpeed * MoveScale;
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

            if (hitbox != null) hitbox.SetActive(false);
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

    // 진행 중인 행동(공격 포함)을 끊고 제자리에 멈춤. 이미 기절 중이면 남은 시간과 새 시간 중 긴 쪽
    public virtual void Stun(float duration)
    {
        if (health.IsDead || duration <= 0f) return;

        if (state == State.Stunned)
        {
            stunRemaining = Mathf.Max(stunRemaining, duration);
        }
        else
        {
            if (hitbox != null) hitbox.SetActive(false);
            state = State.Stunned;
            stateTime = 0f;
            stunRemaining = duration;

            Vector3 velocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, velocity.y, 0f);

            PlayAnimation(State.Stunned);
            OnStunStarted();
        }
    }

    protected virtual void OnStunStarted() { }
    protected virtual void OnStunEnded() { }

    // 넉백 중에는 Update가 멈추므로 기절 시간은 밀려난 뒤부터 흐름
    protected virtual void Stunned()
    {
        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(0f, velocity.y, 0f);

        stunRemaining -= Time.deltaTime;
        if (stunRemaining > 0f) return;

        stunRemaining = 0f;
        state = State.Idle;
        stateTime = 0f;

        PlayAnimation(State.Idle);
        OnStunEnded();
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

        if (hitbox != null) hitbox.SetActive(false);
        state = State.Idle;
        stateTime = 0f;

        PlayAnimation(State.Idle);
    }

    // 사망 시 모든 행동을 멈추고 마지막 포즈로 정지
    // 시체 유지, 제거는 MonsterHealth가 담당
    protected virtual void OnDied()
    {
        if (hitbox != null) hitbox.SetActive(false);

        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(0f, velocity.y, 0f);

        animator.speed = 0f;
        enabled = false;

        SetAttackLoop(false);
        PlaySfx(data.DeathCue);
    }

    // 행동 상태와 같은 애니메이터 상태로 바로 전환
    // 트리거 연쇄를 거치지 않으므로 상태가 연달아 바뀌거나 넉백으로 끊겨도 모션이 어긋나지 않음
    protected void PlayAnimation(State animState)
    {
        animator.CrossFadeInFixedTime(animationHashes[(int)animState], animationFade);
        PlayStateSfx(animState);
    }

    // 행동 상태에 들어설 때의 효과음 (공격 소리를 공격 도중에 내는 몬스터는 재정의, 예: 돌을 던질 때마다)
    protected virtual void PlayStateSfx(State animState)
    {
        SetAttackLoop(animState == State.Attack);
        switch (animState)
        {
            case State.Windup:
                PlaySfx(data.WindupCue);
                break;
            case State.Attack:
                PlaySfx(data.AttackCue);
                PlaySfx(data.RoarCue);
                break;
            case State.Stunned:
                PlaySfx(SfxSetting.Instance?.monsterStun);
                break;
        }
    }

    // 이 몬스터 위치에서 데이터의 음 높이 배율로 재생 (변종은 원본과 다르게 들림)
    protected void PlaySfx(SfxCue cue) => Sfx.Play(cue, transform.position, 1f, data.sfxPitch);

    // 평소(배회·대기)에 가끔 울음소리. 첫 울음 시점은 몬스터마다 흩어 둠
    private float callTimer = -1f;

    protected void UpdateCallSfx()
    {
        SfxCue cue = data.CallCue;
        if (cue == null) return;

        if (callTimer < 0f) callTimer = Random.Range(0f, data.callInterval.y);
        if (!CanCall)
        {
            // 싸움이 끝나자마자 울지 않도록 평소 상태가 된 뒤부터 다시 셈
            callTimer = Mathf.Max(callTimer, data.callInterval.x * 0.5f);
            return;
        }

        callTimer -= Time.deltaTime;
        if (callTimer > 0f) return;
        callTimer = Random.Range(data.callInterval.x, data.callInterval.y);
        PlaySfx(cue);
    }

    protected virtual bool CanCall => state == State.Idle && target == null && !health.IsDead;

    // 공격하는 동안 반복되는 소리 (데이터에 있을 때만, 다른 상태로 넘어가거나 쓰러지면 멈춤)
    private SfxLoop attackLoop;

    protected void SetAttackLoop(bool playing)
    {
        if (attackLoop == null)
        {
            SfxCue cue = data.AttackLoopCue;
            if (!playing || cue == null) return;
            attackLoop = SfxLoop.Ensure(gameObject, cue);
        }
        if (playing) attackLoop.Play();
        else attackLoop.Stop();
    }

    // 애니메이터 상태 이름은 '컨트롤러 이름 + 행동 상태' (예: ShieldboarWindup), 없으면 행동 상태 이름 그대로
    // 변종이 쓰는 오버라이드 컨트롤러는 상태 머신이 원본 것이므로 원본 컨트롤러 이름을 씀
    private void CacheAnimationHashes()
    {
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller is AnimatorOverrideController overrideController && overrideController.runtimeAnimatorController != null)
            controller = overrideController.runtimeAnimatorController;
        string prefix = controller != null ? controller.name : "";
        State[] states = (State[])Enum.GetValues(typeof(State));
        animationHashes = new int[states.Length];

        foreach (State s in states)
        {
            int hash = Animator.StringToHash(prefix + s);
            // 기절 모션이 따로 없으면 Idle 모션을 씀 (Idle이 먼저 채워져 있음)
            if (s == State.Stunned && !animator.HasState(0, hash) && !animator.HasState(0, Animator.StringToHash(s.ToString())))
            {
                animationHashes[(int)s] = animationHashes[(int)State.Idle];
                continue;
            }
            if (!animator.HasState(0, hash))
            {
                hash = Animator.StringToHash(s.ToString());
                if (!animator.HasState(0, hash)) Debug.LogWarning($"[MonsterAI] 애니메이터에 {prefix + s} 상태가 없음: {name}", this);
            }
            animationHashes[(int)s] = hash;
        }
    }
}
