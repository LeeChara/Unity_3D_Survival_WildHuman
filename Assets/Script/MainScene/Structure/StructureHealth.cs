using System;
using UnityEngine;

// 플레이어가 설치한 오브젝트의 체력
// 수치(체력, 도구 배율, 드랍)는 이 설치물을 놓는 PlaceableItemData에서 읽음
// 체력이 높아 일반 공격으로는 잘 부서지지 않고, 맞는 도구(망치)로 공격하면 큰 배율 적용
public class StructureHealth : Health
{
    [Tooltip("이 설치물을 놓는 아이템 (아이템의 placedPrefab이 이 프리팹이어야 함)")]
    [SerializeField] private PlaceableItemData sourceItem;

    public PlaceableItemData SourceItem => sourceItem;

    // 파괴 처리가 모두 끝난 시점을 PlacementManager에 알림 (목록 제거용)
    public event Action<StructureHealth> Destroyed;

    protected override void Awake()
    {
        base.Awake();
        Init(sourceItem.maxHealth);

        if (TryGetComponent(out ItemDropOnDeath dropOnDeath)) dropOnDeath.SetDropTable(sourceItem.dropTable);
    }

    protected override int CalculateDamage(AttackData attack)
    {
        return sourceItem.toolWeakness.CalculateDamage(attack);
    }

    protected override void Die()
    {
        // 아이템 반환은 Died를 구독하는 ItemDropOnDeath가 처리
        base.Die();
        Destroyed?.Invoke(this);
        Destroy(gameObject);
    }
}
