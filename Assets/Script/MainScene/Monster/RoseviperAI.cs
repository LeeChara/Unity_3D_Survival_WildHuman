using UnityEngine;

public class RoseviperAI : MonsterAI, IBurrowable
{
    protected RoseviperData viperData;

    // 목표를 감지하고 매복(Chase) 중일 때만 땅속에 숨은 것으로 봄 → 음파에 즉시 길게 기절
    public virtual bool IsBurrowed => state == State.Chase;
    // 땅속에 있으면 피해를 받지 않음 (음파로 기절시킨 뒤에야 맞음)
    public override bool IsInvulnerable => IsBurrowed && !IsStunned;

    protected override void Awake()
    {
        base.Awake();
        viperData = (RoseviperData)data;

        // 땅속에 있으면 넉백도 받지 않음 (시체는 일반 몬스터처럼 넉백)
        if (knockback != null)
        {
            var baseImmune = knockback.IsImmune;
            knockback.IsImmune = () => (IsBurrowed && !health.IsDead) || (baseImmune != null && baseImmune());
        }
    }

    protected override void Update()
    {
        base.Update();

        // 은신 중에는 고정된 방향을 유지하다가, 기습(Windup/Attack) 순간에만 타겟을 향해 Facing 갱신
        if (target != null && (state == State.Windup || state == State.Attack) && rb.linearVelocity.sqrMagnitude <= 0.01f)
        {
            monsterFacing.UpdateFacing(targetDirection, cameraRight, cameraForwardFlat);
        }
    }

    // 은신 상태: 목표를 감지했지만 공격범위 밖이면 이동 없이 제자리에서 대기 (Facing도 고정)
    protected override void ChaseBehavior()
    {
        rb.linearVelocity = Vector3.zero;
    }

    protected override void Attack()
    {
        AttackBehavior();

        if (hitbox.activeSelf && stateTime > viperData.attackHitDuration)
        {
            // 판정 시간 종료: 명중 여부와 무관하게 히트박스 비활성화
            hitbox.SetActive(false);
        }

        if (hitboxController.HasHit)
        {
            if (stateTime > viperData.attackHitDuration)
            {
                // 공격 성공: attackHitDuration까지만 재생한 뒤 Recover로 전환
                state = State.Recover;
                stateTime = 0f;

                PlayAnimation(State.Recover);
                return;
            }
        }
        else if (stateTime > data.attackDuration)
        {
            // 공격 실패: 전체 지속시간을 다 채운 뒤 Recover로 전환
            state = State.Recover;
            stateTime = 0f;

            PlayAnimation(State.Recover);
        }
    }

    // 기습공격 이후 빠르게 거리를 벌림
    protected override void RecoverBehavior()
    {
        rb.linearVelocity = target != null ? -targetDirection * viperData.retreatSpeed * MoveScale : Vector3.zero;
    }
}
