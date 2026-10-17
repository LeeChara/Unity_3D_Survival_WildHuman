using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// 젖을 수 있는 대상 (체력이 있는 모든 물체: 생물, 자원, 설치물)
// 젖음 규칙 (WaterSetting):
// - 물 속성에 닿으면 젖음 상태가 됨 (생물은 짧게, 그 밖의 물체는 길게). 젖음 자체에는 효과 없이 표시만 함
// - 타는 중에 젖으면 불이 꺼짐 (화약통은 도화선이 꺼짐)
// - 젖은 채로 불이 닿으면 불은 붙지 않고 젖음만 마름 (가연물·화약통은 각자 TryDry로 먼저 확인)
// - 이후 감전·빙결이 이 상태를 조건으로 씀
// - 남은 젖음 시간은 저장됨 (개체는 ISaveable, 자원은 PropSpawner가 청크 데이터에)
[RequireComponent(typeof(Health), typeof(StatusEffectHost))]
public class Wettable : MonoBehaviour, ISaveable
{
    [SerializeField] private WaterSetting setting;
    [Tooltip("젖어 있는 시간 (초). 0이면 WaterSetting의 기본값 (생물/물체)")]
    [SerializeField, Min(0f)] private float duration;
    [Tooltip("어둡게 물들일 피격 반짝임 (비우면 자식에서 찾고, 없으면 자식 그림을 직접 물들임)")]
    [SerializeField] private HitFlash hitFlash;

    // 켜져 있는 모든 대상 (비가 한꺼번에 적실 때 사용)
    private static readonly List<Wettable> active = new();
    public static IReadOnlyList<Wettable> Active => active;

    private Health health;
    private StatusEffectHost host;
    private Combustible combustible;
    private ExplosiveKeg keg;
    private const float EffectCheckInterval = 0.5f;

    private bool shownWet;
    private WetEffect effect;
    private bool effectShown;
    private float effectCheckTimer;
    // 피격 반짝임이 없는 대상의 그림과 원래 색
    private SpriteRenderer[] tintTargets;
    private Color[] tintBase;

    public WaterSetting Setting => setting;
    public bool IsWet => host.Has(setting.wet);
    // 남은 젖음 시간 (젖지 않았으면 0)
    public float Remaining => host.Remaining(setting.wet);
    public float Duration => duration > 0f ? duration
        : health is MonsterHealth || health is PlayerHealth ? setting.creatureWetDuration : setting.objectWetDuration;

    private void Awake()
    {
        health = GetComponent<Health>();
        host = GetComponent<StatusEffectHost>();
        combustible = GetComponent<Combustible>();
        keg = GetComponent<ExplosiveKeg>();
        if (hitFlash == null) hitFlash = GetComponentInChildren<HitFlash>();
        if (hitFlash == null) CollectTintTargets();
    }

    // 풀에서 재사용될 때마다 마른 상태로 시작
    private void OnEnable()
    {
        health.Hit += OnHit;
        active.Add(this);
        SetShown(false);
    }

    private void OnDisable()
    {
        health.Hit -= OnHit;
        active.Remove(this);
    }

    private void Update()
    {
        bool wet = IsWet;
        if (wet != shownWet) SetShown(wet);
        if (!shownWet) return;

        // 화면에 들어오고 나가는 것은 가끔만 확인 (대상마다 시점을 흩어 둠)
        effectCheckTimer -= Time.deltaTime;
        if (effectCheckTimer > 0f) return;
        effectCheckTimer = EffectCheckInterval * Random.Range(0.8f, 1.2f);
        RefreshEffect();
    }

    private void OnHit(AttackData attack)
    {
        // 물을 끼얹었을 때만 첨벙 (비·물웅덩이처럼 계속 적시는 것은 소리 없음)
        if (attack.element == ElementType.Water)
        {
            Sfx.Play(SfxSetting.Instance?.splash, transform.position);
            Effects.PlayOn(setting.splashEffect, transform);
            Soak();
        }
        // 가연물·화약통은 불이 닿을 때 스스로 TryDry로 판단 (붙을지 마를지)
        else if (attack.element == ElementType.Fire && combustible == null && keg == null) TryDry();
    }

    public void Soak() => Soak(Duration);

    // 남은 시간을 지정해서 적심 (저장된 상태 복원용). 이미 젖어 있으면 남은 시간만 바꿈
    public void Soak(float seconds)
    {
        if (health.IsDead || seconds <= 0f) return;

        // 불이 꺼지는 소리는 각자 냄 (Combustible·ExplosiveKeg)
        if (combustible != null) combustible.Extinguish();
        if (keg != null) keg.Defuse();

        host.Apply(setting.wet, seconds);
        if (!shownWet) SetShown(true);
    }

    // 불이 닿았을 때: 젖어 있었으면 말리고 true (이번 불은 붙지 않음)
    public bool TryDry()
    {
        if (!host.Remove(setting.wet)) return false;

        SetShown(false);
        Sfx.Play(SfxSetting.Instance?.extinguish, transform.position, 0.6f);
        Effects.PlayOn(setting.steamEffect, transform);
        return true;
    }

    private void SetShown(bool wet)
    {
        shownWet = wet;
        ApplyTint(wet ? setting.wetTint : Color.white);
        RefreshEffect();
    }

    // 물방울은 화면 근처에서만 보임 (비가 오면 로드된 수천 개가 한꺼번에 젖으므로 멀리 있는 것까지 만들지 않음)
    private void RefreshEffect()
    {
        bool want = shownWet && WetEffect.IsNearView(transform.position);
        if (want == effectShown) return;
        effectShown = want;

        if (!want)
        {
            if (effect != null) effect.Hide();
            return;
        }

        if (effect == null) effect = WetEffect.Attach(setting, transform);
        effect.Show(transform);
    }

    private void ApplyTint(Color tint)
    {
        if (hitFlash != null)
        {
            hitFlash.SetTint(tint);
            return;
        }

        if (tintTargets == null) return;
        for (int i = 0; i < tintTargets.Length; i++)
        {
            if (tintTargets[i] != null) tintTargets[i].color = tintBase[i] * tint;
        }
    }

    // 그림자·상태 아이콘(앞쪽 순서)은 빼고 몸체 그림만
    private void CollectTintTargets()
    {
        var targets = new List<SpriteRenderer>();
        foreach (SpriteRenderer part in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (part.sortingLayerName == "Shadow" || part.sortingOrder >= 100) continue;
            targets.Add(part);
        }

        tintTargets = targets.ToArray();
        tintBase = new Color[tintTargets.Length];
        for (int i = 0; i < tintTargets.Length; i++) tintBase[i] = tintTargets[i].color;
    }

    #region 저장

    public string SaveKey => "wet";

    public JToken Save()
    {
        float remaining = Remaining;
        return remaining > 0f ? new JObject { ["left"] = remaining } : null;
    }

    public void Load(JToken data)
    {
        Soak(data.Value<float>("left"));
    }

    #endregion
}
