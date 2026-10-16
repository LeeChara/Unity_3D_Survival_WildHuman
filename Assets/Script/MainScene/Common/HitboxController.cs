using System.Collections.Generic;
using UnityEngine;

public class HitboxController : MonoBehaviour
{
    [SerializeField] private LayerMask targetLayer; // 공격 대상 레이어
    [SerializeField] private AttackData attack = new AttackData { damage = 10, knockbackDistance = 1f };

    // 한 번의 공격동안 같은 적을 중복으로 공격하지 않도록 추적
    private HashSet<Collider> hitTargets = new HashSet<Collider>();

    // 넉백 방향의 기준점 (공격자 본체)
    private Transform attacker;

    public bool HasHit => hitTargets.Count > 0;

    private void Awake()
    {
        Rigidbody attackerRb = GetComponentInParent<Rigidbody>();
        attacker = attackerRb != null ? attackerRb.transform : transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        // targetLayer에 해당하는 레이어가 아닌 경우 리턴
        if ((targetLayer.value & (1 << other.gameObject.layer)) == 0) return;
        if (hitTargets.Contains(other)) return;

        hitTargets.Add(other);

        // 설치물처럼 콜라이더가 자식에 있는 경우 루트의 체력을 찾음 (자기 자신부터 검사)
        Health health = other.GetComponentInParent<Health>();
        if (health != null)
        {
            health.TakeHit(attack);
            Debug.Log($"{other.name} 피격 - 현재 체력: {health.CurrentHealth} / {health.MaxHealth}");
        }

        // 피해 없는 속성 공격(물 등)은 울림·넉백 없음
        if (attack.IsHarmless) return;

        // 금속 물체에 닿으면 울림. 강타면 때린 쪽의 반대편으로 음파가 퍼짐
        Resonant resonant = other.GetComponentInParent<Resonant>();
        if (resonant != null) resonant.OnAttackHit(attacker.position, attack.strike == StrikeType.Smash);

        if (other.TryGetComponent<Knockback>(out Knockback knockback))
        {
            Vector3 knockbackDir = other.transform.position - attacker.position;
            knockbackDir.y = 0f;
            // 공격자와 위치가 겹치면 히트박스가 바라보는 방향으로 밀어냄
            if (knockbackDir.sqrMagnitude < 0.0001f) knockbackDir = transform.forward;

            knockback.Apply(knockbackDir, attack.knockbackDistance);
        }
    }

    // 공격 종류가 여러 개인 경우 공격을 시작할 때 해당 공격의 수치로 교체
    public void SetAttack(AttackData attack)
    {
        this.attack = attack;
    }

    // 들고 있는 도구에 따라 공격의 피해량과 도구 종류·티어·속성·타격 형태만 교체 (넉백 등 나머지 수치는 유지)
    public void SetWeapon(int damage, ToolType toolType, int toolTier, ElementType element, StrikeType strike)
    {
        attack.damage = damage;
        attack.toolType = toolType;
        attack.toolTier = toolTier;
        attack.element = element;
        attack.strike = strike;
    }

    // 새로운 공격을 시작할 때 초기화
    public void ResetHitTargets()
    {
        hitTargets.Clear();
    }
}