using System;
using UnityEngine;

// 채집 가능한 자원(Prop)의 체력
// 수치(체력, 도구 배율, 드랍, 파괴 이펙트)는 PropData에서 읽음
// 풀링되는 오브젝트라 재사용될 때마다 체력을 다시 채움
public class PropHealth : Health
{
    [SerializeField] private PropData data;

    // 파괴 처리가 모두 끝난 시점을 스포너에 알림 (풀 반환용)
    // Died 구독자(HitFlash 등)가 모두 처리된 뒤에 호출되어야 비활성화 이후 색이 덮어써지지 않음
    public event Action<PropHealth> Depleted;

    public PropData Data => data;
    public override string DisplayName => data.displayName;

    protected override void Awake()
    {
        base.Awake();

        if (TryGetComponent(out ItemDropOnDeath dropOnDeath)) dropOnDeath.SetDropTable(data.dropTable);
    }

    private void OnEnable()
    {
        Init(data.maxHealth);
    }

    // 요구 티어가 있으면 맞는 도구이면서 티어가 충분할 때만 채집 가능 (맨손·다른 도구·몬스터 공격은 불가)
    public bool CanHarvest(ToolType toolType, int toolTier)
    {
        return data.requiredTier == 0 || (toolType == data.toolWeakness.tool && toolTier >= data.requiredTier);
    }

    protected override int CalculateDamage(AttackData attack)
    {
        if (!CanHarvest(attack.toolType, attack.toolTier)) return 0;
        return data.toolWeakness.CalculateDamage(attack);
    }

    protected override void Die()
    {
        SpawnDestroyEffect();

        // 아이템 드랍은 Died를 구독하는 ItemDropOnDeath가 처리
        base.Die();
        Depleted?.Invoke(this);
    }

    // 이펙트는 풀 반환과 함께 비활성화되지 않도록 부모 없이 생성 (이펙트 자체 제거는 이펙트 프리팹이 담당)
    private void SpawnDestroyEffect()
    {
        if (data.destroyEffectPrefab == null) return;

        Instantiate(data.destroyEffectPrefab, transform.position, Quaternion.identity);
    }
}
