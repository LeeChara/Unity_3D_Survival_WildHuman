using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 돌진: 목표 쪽으로 직선 범위를 예고한 뒤 그 방향으로 내달림. 지나가며 닿은 대상은 한 번씩 맞고 옆으로 밀려남
// 기둥·쇠울타리(ChargeBlocker) 같은 단단한 것에 부딪히면 멈추고 기절. 나무 울타리처럼 약한 것은 부수고 계속 달림
// 그 밖의 자원·구조물·월드 경계에 막히면 기절 없이 멈춤
// 2페이즈 불꽃이 타는 동안은 지나간 자리에 불길을 남김
[CreateAssetMenu(fileName = "Charge", menuName = "Monster/Boss Pattern/Charge")]
public class ChargePattern : BossPattern
{
    [Header("예고")]
    [Tooltip("범위 표시가 차오르는 시간 = 예고 시간 (초)")]
    [Min(0f)] public float windup = 1f;
    [Tooltip("돌진 범위 (직선 폭·길이, 길이 = 돌진 거리)")]
    public AttackShape shape = AttackShape.Line(3f, 14f);

    [Header("돌진")]
    [Tooltip("돌진 속도 (m/s)")]
    [Min(0.1f)] public float speed = 16f;
    [Tooltip("몸에 닿았다고 보는 반지름 (m)")]
    [Min(0f)] public float hitRadius = 2.5f;
    public AttackData attack = new AttackData { damage = 25, knockbackDistance = 5f, impactStun = 0.8f };
    [Tooltip("돌진을 막는 레이어 (자원·구조물·월드 경계)")]
    public LayerMask blockLayers = (1 << 8) | (1 << 10) | (1 << 11);

    [Header("2페이즈 화염 장판")]
    [Tooltip("불길을 남기는 간격 (m)")]
    [Min(0.5f)] public float trailSpacing = 1.5f;
    [Tooltip("불길이 타는 시간 (초)")]
    [Min(0.1f)] public float trailLifetime = 6f;

    [Tooltip("돌진이 끝난 뒤 후딜 (초)")]
    [Min(0f)] public float recover = 1.2f;

    private void Reset()
    {
        displayName = "돌진";
        minRange = 5f;
        maxRange = 13f;
        cooldown = 5f;
        weight = 2f;
    }

    public override IEnumerator Run(BossAI boss)
    {
        Vector3 forward = boss.AimAtTarget();
        Vector3 start = boss.transform.position;

        boss.PlayMotion("ChargeWindup", BossMotion.Windup);
        AttackIndicator indicator = boss.ShowIndicator(shape, start, forward, windup);
        yield return new WaitForSeconds(windup);

        boss.PlayMotion("Charge", BossMotion.Attack);

        // 부딪힌 물체는 물리 이벤트에서 받아 다음 프레임에 처리
        Collider blockedBy = null;
        void OnCollided(Collision collision)
        {
            if (blockedBy != null) return;
            if ((blockLayers.value & (1 << collision.gameObject.layer)) == 0) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                // 바닥처럼 위를 향한 면은 제외, 정면을 막는 면만
                Vector3 normal = collision.GetContact(i).normal;
                if (Mathf.Abs(normal.y) > 0.5f) continue;
                normal.y = 0f;
                if (Vector3.Dot(normal.normalized, forward) > -0.3f) continue;
                blockedBy = collision.collider;
                return;
            }
        }

        var hitTargets = new HashSet<Health>();
        var smashed = new HashSet<ChargeBlocker>();
        AttackShape body = AttackShape.Circle(hitRadius);
        float travelled = 0f;
        float crashStun = 0f;
        Vector3 lastTrail = boss.transform.position;
        boss.Collided += OnCollided;

        while (travelled < shape.length)
        {
            Vector3 velocity = forward * speed;
            velocity.y = boss.Body.linearVelocity.y;
            boss.Body.linearVelocity = velocity;
            boss.Strike(body, boss.transform.position, forward, attack, hitTargets);

            Vector3 position = boss.transform.position;
            if (boss.IsFlaming && Vector3.Distance(position, lastTrail) >= trailSpacing)
            {
                boss.SpawnTrailFlame(position, trailLifetime);
                lastTrail = position;
            }

            if (blockedBy != null)
            {
                ChargeBlocker blocker = blockedBy.GetComponentInParent<ChargeBlocker>();
                if (blocker == null) break;
                if (smashed.Add(blocker))
                {
                    blocker.OnCharged(boss.transform.position);
                    if (blocker.Stops)
                    {
                        crashStun = blocker.StunDuration;
                        break;
                    }
                }
                // 부순 물체는 막지 않으므로 계속 달림
                blockedBy = null;
            }

            yield return null;
            travelled += speed * Time.deltaTime;
        }

        boss.Collided -= OnCollided;

        boss.Stop();
        boss.ReleaseIndicator(indicator);

        if (crashStun > 0f)
        {
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.25f, 0.2f);
            // 기절은 패턴을 끊으므로 마지막에
            boss.Stun(crashStun);
            yield break;
        }

        boss.PlayMotion("ChargeRecover", BossMotion.Recover);
        yield return new WaitForSeconds(recover);
    }
}
