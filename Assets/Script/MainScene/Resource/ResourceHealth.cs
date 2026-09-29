using System;
using UnityEngine;

// 채집 가능한 자원(Prop)의 체력
// 맞는 도구로 공격받으면 피해 배율 적용, 체력이 0이 되면 파괴
// 풀링되는 오브젝트라 재사용될 때마다 체력을 다시 채움
public class ResourceHealth : Health
{
    // TODO: 배율 설정은 추후 데이터 에셋으로 이동
    [SerializeField] private ToolType effectiveTool = ToolType.None;
    [SerializeField] private int toolMultiplier = 5;

    [Tooltip("파괴될 때 생성할 이펙트 (비워두면 생성하지 않음)")]
    [SerializeField] private GameObject destroyEffectPrefab;

    // 파괴 처리가 모두 끝난 시점을 스포너에 알림 (풀 반환용)
    // Died 구독자(HitFlash 등)가 모두 처리된 뒤에 호출되어야 비활성화 이후 색이 덮어써지지 않음
    public event Action<ResourceHealth> Depleted;

    private void OnEnable()
    {
        Init(MaxHealth);
    }

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
        SpawnDestroyEffect();
        // TODO: 아이템 드랍

        base.Die();
        Depleted?.Invoke(this);
    }

    // 이펙트는 풀 반환과 함께 비활성화되지 않도록 부모 없이 생성 (이펙트 자체 제거는 이펙트 프리팹이 담당)
    private void SpawnDestroyEffect()
    {
        if (destroyEffectPrefab == null) return;

        Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);
    }
}
