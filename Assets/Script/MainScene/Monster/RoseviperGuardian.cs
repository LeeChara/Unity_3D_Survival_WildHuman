using System.Collections.Generic;
using UnityEngine;

// 월드 구조물의 길목을 막고 있는 Roseviper (예: 가시 덤불 길목)
// 제자리에서 움직이지 않고 넉백도 받지 않으며, 다가오면 일반 Roseviper처럼 기습만 함
// 늘 땅속에 숨은 것으로 봐서 음파에 길게 기절하고, 기절 중에만 피해를 받음
// 기절 중에는 몸과 길목 차단 판정이 플레이어와 부딪히지 않아 지나갈 수 있음
public class RoseviperGuardian : RoseviperAI
{
    [Tooltip("음파로 기절하는 최소 시간 (초). 기둥을 치고 길목을 지나갈 만큼")]
    [SerializeField, Min(0f)] private float minStunDuration = 4.5f;

    // 문지기 자신의 콜라이더 (몸 + 길목 차단 판정인 자식 콜라이더, 트리거인 공격 히트박스 제외)
    private readonly List<Collider> bodyColliders = new();
    // 충돌을 끈 플레이어 콜라이더
    private readonly List<Collider> ignoredPlayerColliders = new();
    // 기절은 끝났지만 플레이어가 아직 길목 안에 있어 충돌을 되돌리지 못한 상태
    private bool restorePending;

    public override bool IsBurrowed => true;
    public override bool IsInvulnerable => !IsStunned;

    protected override void Awake()
    {
        base.Awake();

        // 넉백 중에는 상태머신이 멈추므로 계속 맞으면 공격을 못 하게 됨 → 넉백을 받지 않음
        if (knockback != null) knockback.IsImmune = () => true;
        rb.constraints = RigidbodyConstraints.FreezeAll;

        foreach (var c in GetComponentsInChildren<Collider>(true))
        {
            if (!c.isTrigger) bodyColliders.Add(c);
        }
    }

    protected override void Update()
    {
        base.Update();

        if (restorePending && !PlayerInBlocker()) RestoreCollision();
    }

    public override void Stun(float duration)
    {
        base.Stun(Mathf.Max(duration, minStunDuration));
    }

    protected override void OnStunStarted()
    {
        restorePending = false;
        IgnorePlayer();
    }

    protected override void OnStunEnded()
    {
        restorePending = true;
    }

    // 쓰러진 뒤에는 길이 열린 채로 둠 (시체가 남아 있는 동안에도 지나갈 수 있게)
    protected override void OnDied()
    {
        base.OnDied();
        restorePending = false;
        IgnorePlayer();
    }

    // 제자리 고정: 배회·공격 돌진·후퇴 없이 방향만 바꿈 (회전시키면 차단 판정도 돌아감)
    protected override void Wander()
    {
        rb.linearVelocity = Vector3.zero;
    }

    protected override void WindupBehavior()
    {
        rb.linearVelocity = Vector3.zero;
    }

    protected override void AttackBehavior()
    {
        rb.linearVelocity = Vector3.zero;
    }

    protected override void RecoverBehavior()
    {
        rb.linearVelocity = Vector3.zero;
    }

    // 쫓는 범위 안의 플레이어 콜라이더 전부와 충돌을 끔 (기절 중에 다가오는 경우까지)
    private void IgnorePlayer()
    {
        Collider[] players = Physics.OverlapSphere(transform.position, data.chaseGiveUpRange, playerLayer, QueryTriggerInteraction.Ignore);
        foreach (var player in players)
        {
            if (ignoredPlayerColliders.Contains(player)) continue;

            foreach (var body in bodyColliders) Physics.IgnoreCollision(body, player, true);
            ignoredPlayerColliders.Add(player);
        }
    }

    private void RestoreCollision()
    {
        restorePending = false;
        foreach (var player in ignoredPlayerColliders)
        {
            if (player == null) continue;
            foreach (var body in bodyColliders) Physics.IgnoreCollision(body, player, false);
        }
        ignoredPlayerColliders.Clear();
    }

    // 몸이나 차단 판정 안에 플레이어가 겹쳐 있는지 (그대로 충돌을 되돌리면 튕겨 나감)
    private bool PlayerInBlocker()
    {
        foreach (var body in bodyColliders)
        {
            Bounds bounds = body.bounds;
            if (Physics.CheckBox(bounds.center, bounds.extents + Vector3.one * 0.3f, Quaternion.identity, playerLayer, QueryTriggerInteraction.Ignore)) return true;
        }
        return false;
    }
}
