using System.Collections;
using UnityEngine;

// 광역 찍기: 손을 높게 들어올린 뒤 주변을 원형으로 내려찍음. 크게 밀려나고, 밀려나다 벽에 부딪히면 기절
// 2페이즈: 찍은 뒤 바깥쪽에 도넛 모양 후속타
[CreateAssetMenu(fileName = "GroundSlam", menuName = "Monster/Boss Pattern/Ground Slam")]
public class GroundSlamPattern : BossPattern
{
    [Header("찍기")]
    public AttackShape shape = AttackShape.Circle(6f);
    [Tooltip("손을 들어올린 채 범위가 차오르는 시간 = 예고 시간 (초)")]
    [Min(0f)] public float windup = 1.2f;
    public AttackData attack = new AttackData { damage = 30, knockbackDistance = 6f, impactStun = 0.8f };

    [Header("2페이즈 후속타")]
    public AttackShape followUpShape = AttackShape.Donut(6f, 10f);
    [Tooltip("찍은 뒤 후속타 범위가 차오르는 시간 (초)")]
    [Min(0f)] public float followUpWindup = 0.6f;
    public AttackData followUpAttack = new AttackData { damage = 20, knockbackDistance = 3f };

    [Tooltip("마지막 공격 뒤 후딜 (초)")]
    [Min(0f)] public float recover = 1.2f;

    private void Reset()
    {
        displayName = "광역 찍기";
        maxRange = 5.5f;
        cooldown = 4f;
        weight = 2f;
    }

    public override IEnumerator Run(BossAI boss)
    {
        Vector3 forward = boss.AimAtTarget();
        Vector3 origin = boss.transform.position;

        boss.PlayMotion("SlamWindup", BossMotion.Windup);
        AttackIndicator indicator = boss.ShowIndicator(shape, origin, forward, windup);
        yield return new WaitForSeconds(windup);

        boss.PlayMotion("Slam", BossMotion.Attack);
        boss.Strike(shape, origin, forward, attack);
        boss.ReleaseIndicator(indicator);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.3f, 0.25f);

        if (boss.Phase >= 2)
        {
            AttackIndicator followUp = boss.ShowIndicator(followUpShape, origin, forward, followUpWindup);
            yield return new WaitForSeconds(followUpWindup);

            boss.Strike(followUpShape, origin, forward, followUpAttack);
            boss.ReleaseIndicator(followUp);
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.2f, 0.2f);
        }

        boss.PlayMotion("SlamRecover", BossMotion.Recover);
        yield return new WaitForSeconds(recover);
    }
}
