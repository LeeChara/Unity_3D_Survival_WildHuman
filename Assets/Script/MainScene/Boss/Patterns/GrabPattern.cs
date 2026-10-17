using System.Collections;
using UnityEngine;

// 잡기: 손을 번쩍 들어올린 뒤 정면의 플레이어를 낚아채 잠시 동안 계속 피해를 줌
// 1페이즈는 그 자리에 내려놓고, 2페이즈는 정면으로 던짐 (던져져 벽에 부딪히면 기절)
// 회피 중에는 잡히지 않음. 놓친 경우 짧게 헛손질 후딜
[CreateAssetMenu(fileName = "Grab", menuName = "Monster/Boss Pattern/Grab")]
public class GrabPattern : BossPattern
{
    [Header("낚아채기")]
    [Tooltip("손을 들어올린 채 범위가 차오르는 시간 = 예고 시간 (초)")]
    [Min(0f)] public float windup = 0.8f;
    public AttackShape shape = AttackShape.Sector(3.5f, 70f);
    [Tooltip("잡은 대상을 들고 있는 위치 (보스 앞쪽 거리 m)")]
    [Min(0f)] public float holdDistance = 2.5f;

    [Header("붙잡고 있는 동안")]
    [Tooltip("붙잡고 있는 시간 (초)")]
    [Min(0f)] public float holdDuration = 1.75f;
    [Tooltip("피해를 주는 간격 (초)")]
    [Min(0.05f)] public float tickInterval = 0.25f;
    [Min(0)] public int tickDamage = 8;

    [Header("2페이즈 던지기")]
    public AttackData throwAttack = new AttackData { damage = 15, knockbackDistance = 8f, impactStun = 0.8f };

    [Header("효과음 (손을 들 때 windupCue, 붙잡을 때 attackCue)")]
    public SfxCue throwCue;
    [Tooltip("놓쳤을 때 헛손질")]
    public SfxCue missCue;

    [Header("후딜")]
    [Tooltip("내려놓거나 던진 뒤 (초)")]
    [Min(0f)] public float recover = 0.8f;
    [Tooltip("놓쳤을 때 (초)")]
    [Min(0f)] public float missRecover = 1.2f;

    private void Reset()
    {
        displayName = "잡기";
        maxRange = 3.5f;
        cooldown = 10f;
        weight = 0.7f;
    }

    public override IEnumerator Run(BossAI boss)
    {
        Vector3 forward = boss.AimAtTarget();
        Vector3 origin = boss.transform.position;

        boss.PlayMotion("GrabWindup", BossMotion.Windup);
        boss.PlaySfx(windupCue);
        AttackIndicator indicator = boss.ShowIndicator(shape, origin, forward, windup);
        yield return new WaitForSeconds(windup);

        boss.ReleaseIndicator(indicator);
        IGrabbable grabbed = boss.TryGrab(shape, origin, forward);
        if (grabbed == null)
        {
            boss.PlayMotion("GrabMiss", BossMotion.Recover);
            boss.PlaySfx(missCue);
            yield return new WaitForSeconds(missRecover);
            yield break;
        }

        boss.PlayMotion("GrabHold", BossMotion.Attack);
        boss.PlaySfx(attackCue);
        Health victim = (grabbed as Component).GetComponent<Health>();
        Vector3 holdPoint = boss.transform.position + forward * holdDistance;
        float elapsed = 0f;
        float nextTick = 0f;
        while (elapsed < holdDuration && victim != null && !victim.IsDead)
        {
            grabbed.HoldAt(new Vector3(holdPoint.x, victim.transform.position.y, holdPoint.z));
            if (elapsed >= nextTick)
            {
                // 붙잡힌 동안의 피해도 밤 배율·2페이즈 화염 속성을 따름
                victim.TakeHit(boss.ApplyModifiers(new AttackData { damage = tickDamage }));
                nextTick += tickInterval;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        boss.ReleaseGrab();

        if (boss.Phase >= 2 && victim != null && !victim.IsDead)
        {
            boss.PlayMotion("GrabThrow", BossMotion.Attack);
            boss.PlaySfx(throwCue);
            victim.TakeHit(boss.ApplyModifiers(throwAttack));
            if (victim.TryGetComponent(out Knockback knockback))
                knockback.Apply(forward, throwAttack.knockbackDistance, throwAttack.impactStun);
        }
        else
        {
            boss.PlayMotion("GrabRelease", BossMotion.Recover);
        }

        yield return new WaitForSeconds(recover);
    }
}
