using System.Collections.Generic;
using UnityEngine;

// 트리거보다 실제 위험 범위가 좁은 경우(예: 모양이 불규칙한 용암 호수) 피해 직전에 확인
public interface IHazardArea
{
    bool Contains(Vector3 worldPosition);
}

// 트리거 안에 들어온 생물에게 일정 간격으로 화염 속성 피해를 줌 (용암, 모닥불)
// 불이 붙는 것은 맞은 쪽의 Combustible이 화염 속성을 보고 처리
// 자원·설치물에 불을 옮기는 것은 FireSource가 담당 (움직이지 않는 물체끼리는 트리거 이벤트가 오지 않음)
// 가만히 서 있으면 Rigidbody가 잠들어 OnTriggerStay가 오지 않으므로 들어오고 나간 것만 추적하고 피해는 Update에서 줌
// OnTrigger 메시지는 컴포넌트를 꺼도 오므로, 켜고 끌 때는 이 오브젝트를 비활성화할 것
[RequireComponent(typeof(Collider))]
public class FireHazard : MonoBehaviour
{
    [SerializeField, Min(0)] private int damage = 4;
    [Tooltip("같은 대상에게 다시 피해를 주기까지의 간격 (초)")]
    [SerializeField, Min(0.05f)] private float interval = 0.5f;
    [Tooltip("피해를 받는 레이어 (플레이어, 몬스터)")]
    [SerializeField] private LayerMask targetLayers;

    // 대상별로 겹친 콜라이더 수 (자식 콜라이더가 여러 개일 수 있음)
    private readonly Dictionary<Health, int> inside = new();
    // 잠깐 나갔다 들어와도 간격을 지키도록 나간 뒤에도 기억
    private readonly Dictionary<Health, float> nextHitAt = new();
    private readonly List<Health> buffer = new();
    // 같은 오브젝트에 있으면 트리거 안이어도 이 범위 안일 때만 피해
    private IHazardArea area;

    private void Awake()
    {
        area = GetComponent<IHazardArea>();
    }

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
            if (area != null && !area.Contains(target.transform.position)) continue;
            if (nextHitAt.TryGetValue(target, out float next) && now < next) continue;

            nextHitAt[target] = now + interval;
            target.TakeHit(new AttackData { damage = damage, element = ElementType.Fire });
        }
    }

    private Health GetTarget(Collider other)
    {
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return null;
        return other.GetComponentInParent<Health>();
    }
}
