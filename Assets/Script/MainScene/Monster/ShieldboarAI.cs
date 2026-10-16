using UnityEngine;

public class ShieldboarAI : MonsterAI
{
    [Tooltip("돌진 중 금속 물체에 머리를 박았을 때 기절하는 시간 (초)")]
    [SerializeField, Min(0f)] private float crashStunDuration = 2f;

    // 돌진 중 금속 물체(Resonant)에 부딪히면 강타로 울리고(음파는 반대편으로), 자신은 돌진을 멈추고 기절
    // 공격 판정이 아니라 몸 충돌로 처리 (공격 판정에 설치물을 넣으면 나무 울타리·상자도 피해를 받음)
    // 이미 붙어 있는 상태에서 돌진을 시작해도 처리되도록 Stay도 받음
    private void OnCollisionEnter(Collision collision)
    {
        TryCrash(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryCrash(collision);
    }

    private void TryCrash(Collision collision)
    {
        if (state != State.Attack || health.IsDead) return;

        Resonant resonant = collision.collider.GetComponentInParent<Resonant>();
        if (resonant == null) return;

        resonant.OnAttackHit(transform.position, true);
        Stun(crashStunDuration);
    }
}
