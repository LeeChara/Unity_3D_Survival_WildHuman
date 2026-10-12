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
// - 남은 연소 시간은 저장됨 (개체는 ISaveable, 자원은 PropSpawner가 청크 데이터에)
[RequireComponent(typeof(Health), typeof(StatusEffectHost))]
public class Combustible : MonoBehaviour, ISaveable, IFireReactive
{
    [SerializeField] private CombustionSetting setting;
    [Tooltip("불이 붙었을 때 걸리는 상태 (재질마다 지속 시간·피해가 다름)")]
    [SerializeField] private StatusEffectData burn;
    [Tooltip("타는 동안 켜지는 화염원 (처음에는 꺼 둠)")]
    [SerializeField] private FireSource fireSource;

    [Header("표시 (선택)")]
    [Tooltip("타는 동안 켜지는 불꽃 스프라이트 (생물은 StatusEffectHost의 아이콘을 쓰므로 비워 둠)")]
    [SerializeField] private SpriteRenderer flame;
    [SerializeField] private Sprite[] flameFrames;
    [SerializeField, Min(0.1f)] private float frameRate = 8f;
    [Tooltip("불꽃 색 (알파로 투명도 조절)")]
    [SerializeField] private Color flameColor = new Color(1f, 1f, 1f, 0.85f);
    [Tooltip("타는 동안 켜지는 광원 (처음에는 꺼 둠)")]
    [SerializeField] private LightSource fireLight;
    [Tooltip("그을림 색을 적용할 피격 반짝임 (비우면 그을리지 않음)")]
    [SerializeField] private HitFlash hitFlash;
    [Tooltip("체력이 모두 탔을 때의 색 (깎인 체력만큼 흰색에서 이 색으로 바뀜)")]
    [SerializeField] private Color charredColor = new Color(0.3f, 0.25f, 0.22f);

    private Health health;
    private StatusEffectHost host;
    private bool shownBurning;
    private bool charred;
    private float elapsed;

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
        if (flame != null) flame.color = flameColor;
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
        if (burning != shownBurning) SetShown(burning);
        if (!burning) return;

        elapsed += Time.deltaTime;
        if (flame != null && flameFrames != null && flameFrames.Length > 0)
        {
            flame.sprite = flameFrames[Mathf.FloorToInt(elapsed * frameRate) % flameFrames.Length];
        }

        // 깎인 체력만큼 그을림 (불이 꺼져도 그을린 색은 남음)
        if (hitFlash != null && health.MaxHealth > 0f)
        {
            charred = true;
            hitFlash.SetBaseColor(Color.Lerp(Color.white, charredColor, 1f - health.CurrentHealth / health.MaxHealth));
        }
    }

    private void OnHit(AttackData attack)
    {
        if (attack.element == ElementType.Fire) Ignite();
    }

    public void Ignite() => Ignite(burn.duration);

    public void OnFireContact() => Ignite();

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
        shownBurning = burning;
        elapsed = 0f;
        if (flame != null) flame.enabled = burning;
        if (fireLight != null) fireLight.enabled = burning;
        if (fireSource != null) fireSource.enabled = burning;
        // 쓰러진 뒤에는 HitFlash가 사망 색을 유지하므로 그을림 색을 건드리지 않음
        if (!burning && !charred && hitFlash != null && !health.IsDead) hitFlash.SetBaseColor(Color.white);
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
