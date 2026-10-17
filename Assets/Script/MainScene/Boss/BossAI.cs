using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// 패턴 모션 (애니메이터에 패턴 전용 상태가 없을 때 대신 쓰는 기본 행동 모션)
public enum BossMotion { Idle, Move, Windup, Attack, Recover }

// 보스 행동: 목표가 없으면 배회, 있으면 쫓아가다 조건에 맞는 패턴을 골라 실행
// 목표 탐색·기절·사망·애니메이션 기반은 MonsterAI를 그대로 씀 (공격 판정은 히트박스 대신 패턴이 범위로 직접 처리)
// 체력이 BossData.phase2HealthRatio 이하가 되면 2페이즈 (패턴마다 사용 페이즈·강화 수치가 다름)
public class BossAI : MonsterAI
{
    [Header("보스")]
    [SerializeField] private AttackIndicatorSetting indicatorSetting;
    [Tooltip("몸체 스프라이트 루트 (도약할 때 띄움)")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("2페이즈 불꽃이 켜져 있는 동안 주변 물체에 불을 옮기는 화염원 (플레이어는 제외하도록 설정)")]
    [SerializeField] private FireSource bodyFire;

    // 보스 체력바 등 UI가 플레이어와 싸우기 시작·끝낸 보스를 알 수 있도록 알림
    public static event Action<BossAI> Engaged;
    public static event Action<BossAI> Disengaged;
    // 페이즈가 바뀐 시점 (새 페이즈)
    public event Action<int> PhaseChanged;
    // 몸이 무언가에 부딪힌 시점 (돌진 등 패턴이 실행 중에만 구독, 패턴이 끝나거나 끊기면 모두 해제됨)
    public event Action<Collision> Collided;

    public BossData Boss => (BossData)data;
    public Health Health => health;
    public int Phase { get; private set; } = 1;
    public bool IsEngaged { get; private set; }
    public BossPattern CurrentPattern { get; private set; }

    // 패턴이 읽는 목표 정보
    public Transform Target => target;
    public float TargetDistance => targetDistance;
    public Vector3 TargetDirection => targetDirection;
    public Rigidbody Body => rb;
    public Transform VisualRoot => visualRoot;
    // 패턴이 무적으로 지정되어 있으면 피해를 받지 않음 (예: 포효)
    public override bool IsInvulnerable => CurrentPattern != null && CurrentPattern.invulnerable;
    // 2페이즈 불꽃이 타고 있는지 (젖어 있으면 꺼짐)
    public bool IsFlaming => Phase >= 2 && !health.IsDead && (wettable == null || !wettable.IsWet);
    // 밤 정도 0~1 (낮/밤 배율에 씀)
    public float NightFactor => dayNight != null ? dayNight.NightFactor : 0f;
    // 둥지 중심과 입구 방향 (둥지가 정해 주기 전에는 처음 위치)
    public Vector3 Home { get; private set; }
    private bool hasHome;
    // 둥지로 돌아가다 막혀서 거의 못 움직인 시간 (길찾기가 없으므로 오래 막히면 둥지로 옮김)
    private float stuckTime;
    private float lastHomeDistance;
    private const float StuckWarpTime = 2f;
    public Vector3 HomeEntrance { get; private set; } = Quaternion.Euler(0f, 45f, 0f) * Vector3.back;

    private Coroutine running;
    private readonly List<AttackIndicator> indicators = new();
    private readonly Dictionary<BossPattern, float> readyTimes = new();
    private BossPattern lastPattern;
    // 패턴이 끝난 뒤 다음 패턴을 고르기까지 남은 시간
    private float gapRemaining;
    // 다음에 조건과 상관없이 바로 쓸 패턴 (첫 조우·페이즈 전환의 포효)
    private BossPattern queuedPattern;
    private bool hasRoaredOnce;

    // 잡고 있는 대상
    private IGrabbable grabbed;
    private Vector3 visualRootBase;

    private Wettable wettable;
    private DayNightCycle dayNight;
    private BurnEffect flameEffect;
    private bool shownFlaming;
    private FireRing fireRing;

    // 패턴 전용 애니메이터 상태 해시 (없으면 0)
    private readonly Dictionary<string, int> motionHashes = new();
    private string animatorPrefix;

    protected override void Awake()
    {
        base.Awake();
        health.HealthChanged += OnHealthChanged;
        PlayerDetected += OnPlayerDetected;
        PlayerLost += OnPlayerLost;

        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller is AnimatorOverrideController overrideController && overrideController.runtimeAnimatorController != null)
            controller = overrideController.runtimeAnimatorController;
        animatorPrefix = controller != null ? controller.name : "";

        if (visualRoot != null) visualRootBase = visualRoot.localPosition;
        wettable = GetComponent<Wettable>();
        if (bodyFire != null) bodyFire.enabled = false;
    }

    protected override void Start()
    {
        base.Start();
        if (!hasHome) Home = transform.position;
        // 보스는 실행 중에 생성되므로 씬의 시계를 직접 찾음
        dayNight = FindFirstObjectByType<DayNightCycle>();
    }

    // 둥지가 생성될 때 중심과 입구 방향을 알려 줌
    public void SetHome(Vector3 center, Vector3 entrance)
    {
        Home = center;
        HomeEntrance = entrance;
        hasHome = true;
    }

    // 둥지 밖으로 나간 대상은 쫓지 않음
    protected override bool CanTarget(Transform candidate)
    {
        Vector3 offset = candidate.position - Home;
        offset.y = 0f;
        return offset.sqrMagnitude <= Boss.leashRadius * Boss.leashRadius;
    }

    // 싸우지 않을 때: 둥지에서 멀면 돌아가고, 다친 만큼 회복하고, 다 회복하면 1페이즈로 돌아감
    protected override void Idle()
    {
        if (target == null)
        {
            Recover(Time.deltaTime);

            Vector3 toHome = Home - transform.position;
            toHome.y = 0f;
            float distance = toHome.magnitude;
            if (distance > Boss.homeWanderRadius)
            {
                stuckTime = lastHomeDistance - distance < 0.5f * data.chaseSpeed * Time.deltaTime ? stuckTime + Time.deltaTime : 0f;
                lastHomeDistance = distance;
                if (stuckTime > StuckWarpTime)
                {
                    stuckTime = 0f;
                    rb.position = new Vector3(Home.x, rb.position.y, Home.z);
                    return;
                }

                rb.linearVelocity = toHome.normalized * data.chaseSpeed * MoveScale;
                transform.rotation = Quaternion.LookRotation(toHome);
                return;
            }
            stuckTime = 0f;
            lastHomeDistance = distance;
        }

        base.Idle();
    }

    private void Recover(float deltaTime)
    {
        if (health.IsDead) return;

        if (health.CurrentHealth < health.MaxHealth)
        {
            health.Heal(health.MaxHealth * Boss.regenPerSecond * deltaTime);
            return;
        }
        if (Phase >= 2) ResetPhase();
    }

    // 다 회복해서 처음 상태로 (불꽃·불의 고리를 끄고, 다음 조우에 다시 포효)
    private void ResetPhase()
    {
        Phase = 1;
        hasRoaredOnce = false;
        if (fireRing != null) fireRing.Extinguish();
        fireRing = null;
        PhaseChanged?.Invoke(Phase);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (health != null) health.HealthChanged -= OnHealthChanged;
        ClearIndicators();
        SetEngaged(false);
        if (fireRing != null) fireRing.Extinguish();
    }

    protected override void Update()
    {
        UpdateFlames();

        UpdateTarget();

        if (knockback != null && knockback.IsActive) return;

        stateTime += Time.deltaTime;

        if (target != null)
        {
            targetDistance = Vector3.Distance(transform.position, target.position);
            targetDirection = target.position - transform.position;
            targetDirection.y = 0f;
            targetDirection.Normalize();
        }

        if (state == State.Stunned)
        {
            Stunned();
        }
        else if (running == null)
        {
            if (target == null)
            {
                // 싸움이 끝나면 추격 모션에서 평소 모션으로
                if (state != State.Idle)
                {
                    state = State.Idle;
                    PlayAnimation(State.Idle);
                }
                Idle();
            }
            else
            {
                Think();
            }
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        if (running == null && velocity.sqrMagnitude > 0.01f) Face(velocity);

        animator.SetBool("IsWandering", state == State.Idle && velocity.sqrMagnitude > 0.01f);
    }

    // 쫓아가면서 쓸 수 있는 패턴이 생기면 실행
    private void Think()
    {
        if (queuedPattern != null)
        {
            BossPattern queued = queuedPattern;
            queuedPattern = null;
            running = StartCoroutine(RunPattern(queued));
            return;
        }

        if (gapRemaining > 0f)
        {
            gapRemaining -= Time.deltaTime;
            Stop();
            Face(targetDirection);
            return;
        }

        BossPattern pattern = PickPattern();
        if (pattern != null)
        {
            running = StartCoroutine(RunPattern(pattern));
            return;
        }

        if (state != State.Chase)
        {
            state = State.Chase;
            PlayAnimation(State.Chase);
        }
        rb.linearVelocity = targetDirection * data.chaseSpeed * MoveScale * Mathf.Lerp(1f, Boss.nightMoveScale, NightFactor);
    }

    // 조건에 맞는 패턴 중 가중치로 하나 (직전 패턴은 다른 후보가 있으면 제외)
    private BossPattern PickPattern()
    {
        if (Boss.patterns == null) return null;

        var candidates = new List<BossPattern>();
        float total = 0f;
        foreach (BossPattern pattern in Boss.patterns)
        {
            if (pattern == null || pattern.weight <= 0f || !pattern.IsUsableInPhase(Phase)) continue;
            if (readyTimes.TryGetValue(pattern, out float readyAt) && Time.time < readyAt) continue;
            if (!pattern.CanUse(this)) continue;
            candidates.Add(pattern);
        }
        if (candidates.Count > 1) candidates.Remove(lastPattern);
        if (candidates.Count == 0) return null;

        foreach (BossPattern pattern in candidates) total += pattern.weight;
        float roll = Random.value * total;
        foreach (BossPattern pattern in candidates)
        {
            roll -= pattern.weight;
            if (roll <= 0f) return pattern;
        }
        return candidates[candidates.Count - 1];
    }

    private IEnumerator RunPattern(BossPattern pattern)
    {
        CurrentPattern = pattern;
        Stop();
        Face(targetDirection);

        yield return pattern.Run(this);

        EndPattern(pattern);
    }

    // 패턴이 끝나거나 끊긴 뒤 정리
    private void EndPattern(BossPattern pattern)
    {
        running = null;
        CurrentPattern = null;
        lastPattern = pattern;
        if (pattern != null) readyTimes[pattern] = Time.time + pattern.cooldown;
        gapRemaining = Boss.patternGap * Mathf.Lerp(1f, Boss.nightGapScale, NightFactor);
        ReleaseIndicators();
        ReleaseGrab();
        ResetPose();
        // 끊긴 코루틴은 finally가 실행되지 않으므로 패턴이 구독한 충돌 알림을 여기서 정리
        Collided = null;
        Stop();

        if (state != State.Stunned && !health.IsDead)
        {
            state = target != null ? State.Chase : State.Idle;
            PlayAnimation(state);
        }
    }

    // 진행 중인 패턴을 끊음 (기절·사망·페이즈 전환)
    public void InterruptPattern()
    {
        if (running == null) return;

        StopCoroutine(running);
        EndPattern(CurrentPattern);
    }

    public override void Stun(float duration)
    {
        if (health.IsDead || duration <= 0f) return;

        InterruptPattern();
        base.Stun(duration);
    }

    protected override void OnDied()
    {
        InterruptPattern();
        ClearIndicators();
        base.OnDied();
        SetEngaged(false);
        UpdateFlames();
        if (fireRing != null) fireRing.Extinguish();
        fireRing = null;
    }

    // 2페이즈 불꽃 표시·화염원을 지금 상태에 맞춤 (젖으면 꺼지고 마르면 다시 붙음)
    private void UpdateFlames()
    {
        bool flaming = IsFlaming;
        if (flaming == shownFlaming) return;
        shownFlaming = flaming;

        if (bodyFire != null) bodyFire.enabled = flaming;
        if (flaming)
        {
            if (flameEffect == null && Boss.bodyFlame != null) flameEffect = BurnEffect.Attach(Boss.bodyFlame, transform);
            if (flameEffect != null) flameEffect.Show(transform);
        }
        else if (flameEffect != null)
        {
            flameEffect.Hide();
        }
    }

    private void OnHealthChanged(float current, float max)
    {
        if (Phase >= 2 || health.IsDead || max <= 0f) return;
        if (current / max > Boss.phase2HealthRatio) return;

        Phase = 2;
        PhaseChanged?.Invoke(Phase);

        // 하던 패턴을 끊고 포효부터 (기절 중이면 깨어난 뒤)
        InterruptPattern();
        if (Boss.roarPattern != null) queuedPattern = Boss.roarPattern;

        // 둥지 외곽에 불의 고리 (입구는 비워 둠)
        if (Boss.groundFlame != null && fireRing == null)
        {
            fireRing = FireRing.Spawn(Boss.groundFlame, Home, Boss.fireRingRadius, HomeEntrance, Boss.fireRingGap,
                Boss.fireRingSpacing, Boss.fireRingLightEvery, Boss.fireRingKnockback, transform);
        }
    }

    // 첫 조우 때 한 번 포효
    private void OnPlayerDetected()
    {
        if (!hasRoaredOnce && Boss.roarPattern != null)
        {
            hasRoaredOnce = true;
            queuedPattern = Boss.roarPattern;
        }
        SetEngaged(true);
    }

    private void OnPlayerLost() => SetEngaged(false);

    private void SetEngaged(bool engaged)
    {
        if (IsEngaged == engaged) return;

        IsEngaged = engaged;
        if (engaged) Engaged?.Invoke(this);
        else Disengaged?.Invoke(this);
    }

    private void OnCollisionEnter(Collision collision) => Collided?.Invoke(collision);
    private void OnCollisionStay(Collision collision) => Collided?.Invoke(collision);

    #region 패턴이 쓰는 기능

    // 제자리에 멈춤 (떨어지는 속도는 유지)
    public void Stop()
    {
        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(0f, velocity.y, 0f);
    }

    // 수평 방향으로 몸과 스프라이트를 돌림
    public void Face(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(direction);
        monsterFacing.UpdateFacing(direction, cameraRight, cameraForwardFlat);
    }

    // 목표 쪽으로 돌고 그 방향을 돌려줌 (목표가 없으면 지금 바라보는 방향)
    public Vector3 AimAtTarget()
    {
        if (target == null) return transform.forward;

        Face(targetDirection);
        return targetDirection;
    }

    // 애니메이터에 '컨트롤러 이름 + motion' 또는 motion 상태가 있으면 그 모션, 없으면 기본 행동 모션
    public void PlayMotion(string motion, BossMotion fallback)
    {
        int hash = FindMotion(motion);
        if (hash != 0)
        {
            animator.CrossFadeInFixedTime(hash, 0.1f);
            return;
        }

        PlayAnimation(fallback switch
        {
            BossMotion.Move => State.Chase,
            BossMotion.Windup => State.Windup,
            BossMotion.Attack => State.Attack,
            BossMotion.Recover => State.Recover,
            _ => State.Idle,
        });
    }

    private int FindMotion(string motion)
    {
        if (string.IsNullOrEmpty(motion)) return 0;
        if (motionHashes.TryGetValue(motion, out int cached)) return cached;

        int hash = Animator.StringToHash(animatorPrefix + motion);
        if (!animator.HasState(0, hash))
        {
            hash = Animator.StringToHash(motion);
            if (!animator.HasState(0, hash)) hash = 0;
        }
        motionHashes[motion] = hash;
        return hash;
    }

    // 범위 표시 (패턴이 끝나거나 끊기면 자동으로 사라짐)
    public AttackIndicator ShowIndicator(AttackShape shape, Vector3 origin, Vector3 forward, float duration)
    {
        if (indicatorSetting == null) return null;

        AttackIndicator indicator = AttackIndicator.Show(indicatorSetting, shape, origin, forward, duration);
        indicators.Add(indicator);
        return indicator;
    }

    public void ReleaseIndicator(AttackIndicator indicator)
    {
        if (indicator == null) return;

        indicator.Release();
        indicators.Remove(indicator);
    }

    private void ReleaseIndicators()
    {
        foreach (AttackIndicator indicator in indicators)
        {
            if (indicator != null) indicator.Release();
        }
        indicators.Clear();
    }

    private void ClearIndicators()
    {
        foreach (AttackIndicator indicator in indicators)
        {
            if (indicator != null) Destroy(indicator.gameObject);
        }
        indicators.Clear();
    }

    // 범위 안의 대상(플레이어·적대 몬스터)에게 공격을 줌. 넉백은 기준점에서 바깥쪽으로
    // 맞은 대상 수를 돌려줌
    // exclude: 이미 맞은 대상 (돌진처럼 여러 번 판정하는 공격이 한 번만 맞히도록, 맞힌 대상을 추가함)
    public int Strike(AttackShape shape, Vector3 origin, Vector3 forward, AttackData attack, HashSet<Health> exclude = null)
    {
        List<Health> targets = shape.FindTargets(origin, forward, playerLayer | monsterLayer, transform);
        int count = 0;
        foreach (Health victim in targets)
        {
            if (victim.IsDead) continue;
            // 다른 몬스터는 적대 관계일 때만 (같은 편 몬스터는 맞지 않음)
            if (victim.TryGetComponent(out MonsterAI other) && !data.IsHostileTo(other.Data)) continue;
            if (exclude != null && !exclude.Add(victim)) continue;

            // 피해·속성이 없는 공격(포효)은 밀어내기만
            if (attack.damage > 0 || attack.element != ElementType.None) victim.TakeHit(ApplyModifiers(attack));
            count++;

            if (attack.knockbackDistance > 0f && victim.TryGetComponent(out Knockback victimKnockback))
            {
                Vector3 direction = victim.transform.position - origin;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.0001f) direction = forward;
                victimKnockback.Apply(direction, attack.knockbackDistance, attack.impactStun);
            }
        }
        return count;
    }

    // 2페이즈 불꽃이 타면 화염 속성, 밤에는 피해 감소 (보스의 모든 공격에 적용)
    public AttackData ApplyModifiers(AttackData attack)
    {
        if (attack.damage > 0) attack.damage = Mathf.Max(1, Mathf.RoundToInt(attack.damage * Mathf.Lerp(1f, Boss.nightDamageScale, NightFactor)));
        if (IsFlaming && attack.damage > 0) attack.element = ElementType.Fire;
        return attack;
    }

    // 2페이즈 돌진이 지나간 자리에 남기는 불길
    public void SpawnTrailFlame(Vector3 position, float lifetime)
    {
        if (Boss.groundFlame == null) return;
        GroundFlame.Spawn(Boss.groundFlame, new Vector3(position.x, 0f, position.z), lifetime, transform, true);
    }

    // 범위 안의 잡을 수 있는 대상 하나를 붙잡음 (없거나 회피 중이면 null)
    public IGrabbable TryGrab(AttackShape shape, Vector3 origin, Vector3 forward)
    {
        foreach (Health victim in shape.FindTargets(origin, forward, playerLayer, transform))
        {
            if (victim.IsDead || !victim.TryGetComponent(out IGrabbable grabbable)) continue;
            if (!grabbable.TryGrab()) continue;

            grabbed = grabbable;
            return grabbable;
        }
        return null;
    }

    // 잡고 있는 대상을 내려놓음 (패턴이 끝나거나 끊기면 자동으로 호출)
    public void ReleaseGrab()
    {
        if (grabbed == null) return;

        grabbed.Release();
        grabbed = null;
    }

    // 도약 등으로 바꾼 몸 상태를 되돌림
    public void ResetPose()
    {
        if (rb.isKinematic) rb.isKinematic = false;
        if (visualRoot != null) visualRoot.localPosition = visualRootBase;
    }

    #endregion
}
