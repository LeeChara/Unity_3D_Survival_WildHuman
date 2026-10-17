using System;
using UnityEngine;

// 채집 가능한 자원(Prop)의 체력
// 수치(체력, 도구 배율, 드랍, 피격·파괴 이펙트)는 PropData에서 읽음
// 풀링되는 오브젝트라 재사용될 때마다 체력을 다시 채움
public class PropHealth : Health
{
    [SerializeField] private PropData data;
    [Tooltip("나타난 직후 이 시간(초) 동안은 피해를 받지 않음 (나무를 쓰러뜨린 공격이 그 자리에 남은 밑동까지 때리지 않도록)")]
    [SerializeField, Min(0f)] private float spawnGrace = 0.4f;

    // 파괴 처리가 모두 끝난 시점을 스포너에 알림 (풀 반환용)
    // Died 구독자(HitFlash 등)가 모두 처리된 뒤에 호출되어야 비활성화 이후 색이 덮어써지지 않음
    public event Action<PropHealth> Depleted;

    // 무기로 자원을 때릴 때 피해 배율 (무기가 채집 도구보다 강해지지 않도록)
    private const float WeaponDamageScale = 0.2f;

    private ItemDropOnDeath dropOnDeath;
    private Combustible combustible;
    private float spawnedAt;

    public PropData Data => data;
    // 타는 중에 쓰러졌는지 (밑동을 남기지 않는 등 일반 파괴와 구분)
    public bool BurnedOut => combustible != null && combustible.DiedBurning;
    public Combustible Combustible => combustible;
    public override string DisplayName => combustible != null && combustible.IsBurning ? $"{data.displayName} (불타는 중)" : data.displayName;

    protected override void Awake()
    {
        base.Awake();

        dropOnDeath = GetComponent<ItemDropOnDeath>();
        combustible = GetComponent<Combustible>();
        Hit += OnHit;
    }

    private void OnEnable()
    {
        spawnedAt = Time.time;
        Init(data.maxHealth);
        if (dropOnDeath != null) dropOnDeath.SetDropTable(data.dropTable);
    }

    // 요구 티어가 있으면 맞는 도구이면서 티어가 충분할 때만 채집 가능 (맨손·다른 도구·몬스터 공격은 불가)
    public bool CanHarvest(ToolType toolType, int toolTier)
    {
        return data.requiredTier == 0 || (toolType == data.toolWeakness.tool && toolTier >= data.requiredTier);
    }

    protected override int CalculateDamage(AttackData attack)
    {
        if (Time.time - spawnedAt < spawnGrace) return 0;
        if (!CanHarvest(attack.toolType, attack.toolTier)) return 0;
        int damage = data.toolWeakness.CalculateDamage(attack);
        if (attack.isWeapon && data.reduceWeaponDamage) damage = Mathf.RoundToInt(damage * WeaponDamageScale);
        return damage;
    }

    // 피해 없는 속성 공격(물 뿌리기 등)은 파편을 튀기지 않음
    private void OnHit(AttackData attack)
    {
        if (attack.IsHarmless || IsDead) return;

        Effects.PlayOn(data.hitEffect, transform, data.effectColor);
        // 도구 티어가 모자라 깎이지 않으면 튕겨 나는 소리
        SfxCue cue = CanHarvest(attack.toolType, attack.toolTier) ? data.HitCue : SfxSetting.Instance?.hitBlocked;
        Sfx.Play(cue, transform.position);
    }

    protected override void Die()
    {
        Effects.PlayOn(data.destroyEffect, transform, data.effectColor);
        Sfx.Play(data.BreakCue, transform.position);

        // 아이템 드랍은 Died를 구독하는 ItemDropOnDeath가 처리
        base.Die();
        Depleted?.Invoke(this);
    }
}
