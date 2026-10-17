using System;
using System.Collections;
using UnityEngine;

// 연속 할퀴기: 정해진 타격을 차례로 (예: 왼손 → 오른손 → 양손 마무리) 이어간 뒤 후딜
// 타격마다 목표 쪽으로 다시 돌아서 범위를 표시하고, 다 차오르면 그 범위로 공격
[CreateAssetMenu(fileName = "ClawCombo", menuName = "Monster/Boss Pattern/Claw Combo")]
public class ClawComboPattern : BossPattern
{
    [Serializable]
    public struct Strike
    {
        [Tooltip("애니메이터 상태 이름 (없으면 기본 공격 모션)")]
        public string motion;
        public AttackShape shape;
        [Tooltip("범위 표시가 차오르는 시간 = 예고 시간 (초)")]
        [Min(0f)] public float windup;
        [Tooltip("공격 뒤 다음 타격으로 넘어가기까지 (초)")]
        [Min(0f)] public float after;
        [Tooltip("공격하면서 앞으로 내딛는 거리 (m)")]
        [Min(0f)] public float step;
        public AttackData attack;
        [Tooltip("2페이즈 피해량 (0이면 1페이즈와 같음)")]
        [Min(0)] public int phase2Damage;
    }

    public Strike[] strikes =
    {
        new Strike { motion = "ClawLeft", shape = AttackShape.Sector(3.5f, 120f), windup = 0.5f, after = 0.15f, step = 0.5f,
            attack = new AttackData { damage = 12, knockbackDistance = 0.5f }, phase2Damage = 15 },
        new Strike { motion = "ClawRight", shape = AttackShape.Sector(3.5f, 120f), windup = 0.3f, after = 0.15f, step = 0.5f,
            attack = new AttackData { damage = 12, knockbackDistance = 0.5f }, phase2Damage = 15 },
        new Strike { motion = "ClawBoth", shape = AttackShape.Sector(4f, 160f), windup = 0.5f, after = 0f, step = 0.8f,
            attack = new AttackData { damage = 18, knockbackDistance = 2.5f }, phase2Damage = 22 },
    };

    [Tooltip("마지막 타격 효과음 (나머지 타격은 attackCue, 타격마다 손을 들 때 windupCue)")]
    public SfxCue finisherCue;

    [Tooltip("마지막 타격 뒤 후딜 (초)")]
    [Min(0f)] public float recover = 1f;

    private void Reset()
    {
        displayName = "3연속 할퀴기";
        maxRange = 4f;
        weight = 3f;
    }

    public override IEnumerator Run(BossAI boss)
    {
        for (int i = 0; i < strikes.Length; i++)
        {
            Strike strike = strikes[i];
            bool last = i == strikes.Length - 1;
            Vector3 forward = boss.AimAtTarget();
            boss.PlayMotion(strike.motion + "Windup", BossMotion.Windup);
            boss.PlaySfx(windupCue);

            // 범위는 내딛은 뒤의 위치 기준 (표시와 판정이 같은 자리)
            Vector3 origin = boss.transform.position + forward * strike.step;
            AttackIndicator indicator = boss.ShowIndicator(strike.shape, origin, forward, strike.windup);
            yield return new WaitForSeconds(strike.windup);

            boss.PlayMotion(strike.motion, BossMotion.Attack);
            boss.PlaySfx(last ? SfxCue.Or(finisherCue, attackCue) : attackCue);
            if (strike.step > 0f) boss.Body.MovePosition(boss.Body.position + forward * strike.step);

            AttackData attack = strike.attack;
            if (boss.Phase >= 2 && strike.phase2Damage > 0) attack.damage = strike.phase2Damage;
            boss.Strike(strike.shape, origin, forward, attack);
            boss.ReleaseIndicator(indicator);

            if (strike.after > 0f) yield return new WaitForSeconds(strike.after);
        }

        boss.PlayMotion("ClawRecover", BossMotion.Recover);
        yield return new WaitForSeconds(recover);
    }
}
