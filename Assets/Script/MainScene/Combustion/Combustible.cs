using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// 탈 수 있는 대상 (이 컴포넌트가 있으면 가연성. 플레이어에게는 따로 표시하지 않음)
// 연소 규칙 (모든 대상 공통):
// - 화염 속성 공격에 맞으면 불이 붙어 burn 상태 동안 지속 피해 (다시 맞으면 남은 시간만 갱신)
// - 타는 동안 스스로 화염원(FireSource)이 되어 맞닿은 대상에 불을 옮김
// - 타는 중에 쓰러지면 (원인과 관계없이) 드랍이 구운 형태로 바뀜 (CombustionSetting의 굽기 표, 구울 수 없으면 그대로)
//   설치물은 자기 자신 대신 제작 재료로 분해된 뒤 구워짐 (예: 울타리 → 나무 → 숯)
//   일부 재료는 바뀌면서 개수가 줄어듦 (CombustionSetting.burnYields, 예: 나무 → 숯 절반)
//   보관함 속 아이템은 한 개씩 일정 확률로 사라지고 남은 것만 구워짐 (StorageChest가 BurnStored 사용)
// - 젖은 대상(Wettable)은 불이 닿아도 붙지 않고 젖음만 마름. 타는 중에 젖으면 불이 꺼짐
// - 남은 연소 시간은 저장됨 (개체는 ISaveable, 자원은 PropSpawner가 청크 데이터에)
[RequireComponent(typeof(Health), typeof(StatusEffectHost))]
public class Combustible : MonoBehaviour, ISaveable, IFireReactive
{
    [SerializeField] private CombustionSetting setting;
    [Tooltip("불이 붙었을 때 걸리는 상태 (재질마다 지속 시간·피해가 다름)")]
    [SerializeField] private StatusEffectData burn;
    [Tooltip("타는 동안 켜지는 화염원 (처음에는 꺼 둠)")]
    [SerializeField] private FireSource fireSource;

    [Header("표시")]
    [Tooltip("그을림 색을 적용할 피격 반짝임 (비우면 그을리지 않음). 불꽃·빛은 CombustionSetting.burnEffect가 대상 크기에 맞춰 덮음")]
    [SerializeField] private HitFlash hitFlash;
    [Tooltip("체력이 모두 탔을 때의 색 (깎인 체력만큼 흰색에서 이 색으로 바뀜)")]
    [SerializeField] private Color charredColor = new Color(0.3f, 0.25f, 0.22f);
    [Tooltip("불꽃 크기를 잴 때 빼는 그림 (예: 손에 든 장비)")]
    [SerializeField] private SpriteRenderer[] coverIgnore;

    private Health health;
    private StatusEffectHost host;
    private Wettable wettable;
    private bool shownBurning;
    private bool charred;
    private BurnEffect effect;
    // 애니메이션으로 자세가 바뀌는 대상의 기본 자세 그림 범위 (대상 기준 로컬 중심, 월드 크기). 없으면 불붙을 때마다 잼
    private Bounds? restCover;

    public CombustionSetting Setting => setting;
    public StatusEffectData Burn => burn;
    public bool IsBurning => host.Has(burn);
    // 남은 연소 시간 (타지 않으면 0)
    public float Remaining => host.Remaining(burn);
    // 쓰러지는 순간 타고 있었는지 (드랍 변환, 밑동을 남기지 않는 등에 사용)
    public bool DiedBurning { get; private set; }

    private void Awake()
    {
        health = GetComponent<Health>();
        host = GetComponent<StatusEffectHost>();
        wettable = GetComponent<Wettable>();

        // 생물(플레이어·몬스터)은 구르기·공격 자세에 따라 그림 범위가 바뀌므로 생성 시점의 기본 자세로 한 번만 잼
        if (GetComponentInChildren<Animator>() != null && BurnEffect.TryMeasure(transform, null, coverIgnore, out Bounds cover))
        {
            restCover = new Bounds(transform.InverseTransformPoint(cover.center), cover.size);
        }
    }

    // 풀에서 재사용될 때마다 불이 꺼진 원래 상태로 시작
    private void OnEnable()
    {
        health.Hit += OnHit;
        health.Dying += OnDying;
        health.Revived += OnRevived;

        DiedBurning = false;
        charred = false;
        SetShown(false);
        if (hitFlash != null) hitFlash.SetBaseColor(Color.white);
    }

    private void OnDisable()
    {
        health.Hit -= OnHit;
        health.Dying -= OnDying;
        health.Revived -= OnRevived;
    }

    private void Update()
    {
        bool burning = IsBurning;
        if (shownBurning && !burning) PlayExtinguish();
        if (burning != shownBurning) SetShown(burning);
        if (!burning) return;

        // 깎인 체력만큼 그을림 (불이 꺼져도 그을린 색은 남음)
        if (hitFlash != null && health.MaxHealth > 0f)
        {
            charred = true;
            hitFlash.SetBaseColor(Color.Lerp(Color.white, charredColor, 1f - health.CurrentHealth / health.MaxHealth));
        }
    }

    private void OnHit(AttackData attack)
    {
        if (attack.element == ElementType.Fire) CatchFire();
    }

    public void Ignite() => Ignite(burn.duration);

    public void OnFireContact() => CatchFire();

    // 불이 닿음: 젖어 있으면 붙지 않고 젖음만 마름
    private void CatchFire()
    {
        if (wettable != null && wettable.TryDry()) return;
        Ignite();
    }

    // 불을 끔 (예: 타는 중에 젖음)
    public void Extinguish()
    {
        if (!host.Remove(burn)) return;

        PlayExtinguish();
        SetShown(false);
    }

    private void PlayExtinguish()
    {
        if (health.IsDead) return;
        Effects.PlayOn(setting.extinguishEffect, transform);
        Sfx.Play(SfxSetting.Instance?.extinguish, transform.position);
    }

    // 남은 시간을 지정해서 붙임 (저장된 상태 복원용)
    public void Ignite(float duration)
    {
        if (health.IsDead || duration <= 0f) return;

        host.Apply(burn, duration);
        if (!shownBurning) SetShown(true);
    }

    private void OnDying()
    {
        DiedBurning = IsBurning;
    }

    private void OnRevived()
    {
        DiedBurning = false;
    }

    private void SetShown(bool burning)
    {
        if (burning && !shownBurning) Sfx.Play(SfxSetting.Instance?.ignite, transform.position);
        SetBurnSound(burning);
        shownBurning = burning;
        if (fireSource != null) fireSource.enabled = burning;
        SetEffectVisible(burning);
        // 쓰러진 뒤에는 HitFlash가 사망 색을 유지하므로 그을림 색을 건드리지 않음
        if (!burning && !charred && hitFlash != null && !health.IsDead) hitFlash.SetBaseColor(Color.white);
    }

    // 타는 동안 반복되는 소리 (처음 탈 때 붙이고 켜고 끔)
    private SfxLoop burnLoop;

    private void SetBurnSound(bool burning)
    {
        if (burnLoop == null)
        {
            if (!burning || SfxSetting.Instance == null || SfxSetting.Instance.burnLoop == null) return;
            burnLoop = SfxLoop.Ensure(gameObject, SfxSetting.Instance.burnLoop);
        }
        if (burning) burnLoop.Play();
        else burnLoop.Stop();
    }

    // 불꽃 이펙트는 처음 탈 때 한 번 만들어 자식으로 두고 켜고 끔
    private void SetEffectVisible(bool visible)
    {
        if (!visible)
        {
            if (effect != null) effect.Hide();
            return;
        }

        if (effect == null)
        {
            if (setting.burnEffect == null) return;
            effect = BurnEffect.Attach(setting.burnEffect, transform);
        }
        effect.Show(transform, restCover);
    }

    #region 형태 변환

    // 보관함 속 아이템: 타는 중에 부서졌으면 일부가 사라지고 남은 것은 구워짐
    public (ItemData item, int count) BurnStored(ItemData item, int count)
    {
        if (!DiedBurning) return (item, count);
        return (setting.Cook(item), setting.SurvivingCount(count));
    }

    // 불타 쓰러졌을 때의 드랍: 설치물은 제작 재료로 분해 후 굽고, 나머지는 드랍 표의 항목을 구움 (재료에 따라 개수 손실)
    public void DropBurned(DropTable table, Vector3 origin)
    {
        ItemDropper dropper = ItemDropper.Instance;
        if (dropper == null) return;

        if (TryGetComponent(out StructureHealth structure))
        {
            List<Ingredient> ingredients = setting.IngredientsOf(structure.SourceItem);
            if (ingredients != null)
            {
                foreach (Ingredient ingredient in ingredients)
                {
                    DropBurnedItem(dropper, ingredient.item, ingredient.count, origin);
                }
                return;
            }
        }

        if (table == null || table.Drops == null) return;
        foreach (ItemDrop drop in table.Drops)
        {
            if (drop.item != null) DropBurnedItem(dropper, drop.item, drop.Roll(), origin);
        }
    }

    private void DropBurnedItem(ItemDropper dropper, ItemData item, int count, Vector3 origin)
    {
        int burned = setting.ApplyYield(item, count);
        if (burned > 0) dropper.Drop(setting.Cook(item), burned, origin);
    }

    #endregion

    #region 저장

    public string SaveKey => "burn";

    public JToken Save()
    {
        float remaining = Remaining;
        return remaining > 0f ? new JObject { ["left"] = remaining } : null;
    }

    public void Load(JToken data)
    {
        Ignite(data.Value<float>("left"));
    }

    #endregion
}
