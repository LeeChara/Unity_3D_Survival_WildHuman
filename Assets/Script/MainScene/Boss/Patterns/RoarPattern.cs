using System.Collections;
using UnityEngine;

// 포효: 첫 조우와 페이즈 전환 때 한 번씩 (BossData.roarPattern, 다른 패턴처럼 고르지 않음)
// 피해 없이 주변을 밀어내기만 함. 포효하는 동안 보스는 피해를 받지 않음 (invulnerable)
[CreateAssetMenu(fileName = "Roar", menuName = "Monster/Boss Pattern/Roar")]
public class RoarPattern : BossPattern
{
    [Tooltip("몸을 일으키는 시간 = 예고 시간 (초)")]
    [Min(0f)] public float windup = 0.6f;
    public AttackShape shape = AttackShape.Circle(8f);
    [Tooltip("밀어내는 거리 (피해 없음)")]
    public AttackData attack = new AttackData { damage = 0, knockbackDistance = 5f };
    [Tooltip("포효를 유지하는 시간 (초)")]
    [Min(0f)] public float duration = 1.2f;

    private void Reset()
    {
        displayName = "포효";
        usableInPhase1 = true;
        usableInPhase2 = true;
        invulnerable = true;
        weight = 0f;
    }

    public override IEnumerator Run(BossAI boss)
    {
        Vector3 forward = boss.AimAtTarget();
        Vector3 origin = boss.transform.position;

        boss.PlayMotion("RoarWindup", BossMotion.Windup);
        boss.PlaySfx(windupCue);
        AttackIndicator indicator = boss.ShowIndicator(shape, origin, forward, windup);
        yield return new WaitForSeconds(windup);

        boss.PlayMotion("Roar", BossMotion.Attack);
        boss.PlaySfx(attackCue);
        boss.Strike(shape, origin, forward, attack);
        boss.ReleaseIndicator(indicator);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.35f, duration);

        yield return new WaitForSeconds(duration);
    }
}
