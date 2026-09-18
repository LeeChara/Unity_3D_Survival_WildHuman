using UnityEngine;

public class RoseviperAI : MonsterAI
{
    protected RoseviperData viperData;

    protected override void Awake()
    {
        base.Awake();
        viperData = (RoseviperData)data;
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

        if (stateTime <= viperData.attackHitDuration)
        {
            if (hitboxController.HasHit)
            {
                // 공격 성공: 즉시 Recover로 전환
                state = State.Recover;
                stateTime = 0f;

                animator.SetTrigger("Recover");
                hitbox.SetActive(false);
                return;
            }
        }
        else if (hitbox.activeSelf)
        {
            // 판정 시간 종료: 명중 여부와 무관하게 히트박스 비활성화
            hitbox.SetActive(false);
        }

        if (stateTime > data.attackDuration)
        {
            // 공격 실패: 전체 지속시간을 다 채운 뒤 Recover로 전환
            state = State.Recover;
            stateTime = 0f;

            animator.SetTrigger("Recover");
        }
    }

    // 기습공격 이후 빠르게 거리를 벌림
    protected override void RecoverBehavior()
    {
        rb.linearVelocity = target != null ? -targetDirection * viperData.retreatSpeed : Vector3.zero;
    }
}
