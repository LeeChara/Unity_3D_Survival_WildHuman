using System.Collections.Generic;
using UnityEngine;

// 가시가 있는 물체(장미 덤불)에 닿은 생물에게 일정 간격으로 피해를 주고 바깥으로 밀어냄
// 단단한 판정보다 조금 큰 트리거에 붙임 (부딪혀 멈춘 상태에서도 트리거 안에 있도록)
// FireHazard와 같은 이유로 들어오고 나간 것만 추적하고 피해는 Update에서 줌
// Roseviper는 장미 덤불 속에 사는 뱀이라 가시에 다치지 않음
[RequireComponent(typeof(Collider))]
public class ThornHazard : MonoBehaviour
{
    [SerializeField, Min(0)] private int damage = 5;
    [Tooltip("덤불 중심에서 바깥쪽으로 밀어내는 거리 (m)")]
    [SerializeField, Min(0f)] private float knockbackDistance = 2f;
    [Tooltip("같은 대상에게 다시 피해를 주기까지의 간격 (초)")]
    [SerializeField, Min(0.05f)] private float interval = 0.5f;
    [Tooltip("피해를 받는 레이어 (플레이어, 몬스터)")]
    [SerializeField] private LayerMask targetLayers;

    // 대상별로 겹친 콜라이더 수 (자식 콜라이더가 여러 개일 수 있음)
    private readonly Dictionary<Health, int> inside = new();
    // 잠깐 나갔다 들어와도 간격을 지키도록 나간 뒤에도 기억
    private readonly Dictionary<Health, float> nextHitAt = new();
    private readonly List<Health> buffer = new();

    private void OnDisable()
    {
        inside.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        Health target = GetTarget(other);
        if (target == null) return;

        inside.TryGetValue(target, out int count);
        inside[target] = count + 1;
    }

    private void OnTriggerExit(Collider other)
    {
        Health target = GetTarget(other);
        if (target == null || !inside.TryGetValue(target, out int count)) return;

        if (count <= 1) inside.Remove(target);
        else inside[target] = count - 1;
    }

    private void Update()
    {
        if (inside.Count == 0) return;

        // 피해 중에 대상이 쓰러지거나 비활성화되면 목록이 바뀔 수 있으므로 복사해서 순회
        buffer.Clear();
        buffer.AddRange(inside.Keys);

        float now = Time.time;
        foreach (Health target in buffer)
        {
            // 파괴·비활성화된 대상은 OnTriggerExit가 오지 않으므로 여기서 정리
            if (target == null || !target.isActiveAndEnabled)
            {
                inside.Remove(target);
                nextHitAt.Remove(target);
                continue;
            }
            if (target.IsDead) continue;
            if (nextHitAt.TryGetValue(target, out float next) && now < next) continue;

            nextHitAt[target] = now + interval;
            target.TakeHit(new AttackData { damage = damage });
            if (target.TryGetComponent(out Knockback knockback)) knockback.Apply(target.transform.position - transform.position, knockbackDistance);
        }
    }

    private Health GetTarget(Collider other)
    {
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return null;
        if (other.GetComponentInParent<RoseviperAI>() != null) return null;
        return other.GetComponentInParent<Health>();
    }
}
