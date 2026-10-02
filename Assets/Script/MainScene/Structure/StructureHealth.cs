using System;
using UnityEngine;

// 플레이어가 설치한 오브젝트의 체력
// 체력이 높아 일반 공격으로는 잘 부서지지 않고, 맞는 도구(망치)로 공격하면 큰 배율 적용
public class StructureHealth : Health
{
    // TODO: 배율 설정은 추후 데이터 에셋으로 이동 (ResourceHealth와 함께)
    [SerializeField] private ToolType effectiveTool = ToolType.Hammer;
    [SerializeField] private int toolMultiplier = 100;

    // 파괴 처리가 모두 끝난 시점을 PlacementManager에 알림 (목록 제거용)
    public event Action<StructureHealth> Destroyed;

    protected override int CalculateDamage(AttackData attack)
    {
        if (attack.toolType != ToolType.None && attack.toolType == effectiveTool)
        {
            return attack.damage * toolMultiplier;
        }
        return attack.damage;
    }

    protected override void Die()
    {
        // 아이템 반환은 Died를 구독하는 ItemDropOnDeath가 처리
        base.Die();
        Destroyed?.Invoke(this);
        Destroy(gameObject);
    }
}
