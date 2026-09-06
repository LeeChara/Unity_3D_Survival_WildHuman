using System.Collections.Generic;
using UnityEngine;

public class HitboxController : MonoBehaviour
{
    [SerializeField] private LayerMask targetLayer; // 공격 대상 레이어
    [SerializeField] private int damage = 10;

    // 한 번의 공격동안 같은 적을 중복으로 공격하지 않도록 추적
    private HashSet<Collider> hitTargets = new HashSet<Collider>();

    private void OnTriggerEnter(Collider other)
    {
        // targetLayer에 해당하는 레이어가 아닌 경우 리턴
        if ((targetLayer.value & (1 << other.gameObject.layer)) == 0) return;
        if (hitTargets.Contains(other)) return;

        hitTargets.Add(other);

        if (other.TryGetComponent<Health>(out Health health))
        {
            health.TakeDamage(damage);
        }
    }

    // 새로운 공격을 시작할 때 초기화
    public void ResetHitEnemies()
    {
        hitTargets.Clear();
    }
}