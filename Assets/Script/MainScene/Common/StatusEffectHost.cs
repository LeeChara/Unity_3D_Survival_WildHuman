using System;
using System.Collections.Generic;
using UnityEngine;

// 상태 이상을 받는 쪽 (플레이어, 몬스터)
// 속성 공격에 맞으면 그 속성에 연결된 상태가 걸림 (예: 화염 → 화상), 같은 상태는 중첩 없이 지속 시간만 갱신
// 지속 피해는 TakeDamage로 주므로 피격 반짝임은 나오지만 다시 상태를 거는 연쇄는 없음
// 사망·부활 시 모두 해제, 상태는 저장하지 않음
[RequireComponent(typeof(Health))]
public class StatusEffectHost : MonoBehaviour
{
    [Tooltip("이 대상이 걸릴 수 있는 상태 (각 상태의 appliedBy 속성 공격에 맞으면 걸림)")]
    [SerializeField] private StatusEffectData[] reactions;
    [Tooltip("걸린 상태를 보여줄 스프라이트 (가장 최근에 걸린 상태의 프레임을 재생)")]
    [SerializeField] private SpriteRenderer indicator;

    private class ActiveEffect
    {
        public StatusEffectData data;
        public float remaining;
        public float tickTimer;
        public float elapsed;
    }

    private readonly List<ActiveEffect> active = new();
    private Health health;

    // 상태가 걸리거나 풀린 시점을 UI 등 외부 시스템에 알림
    public event Action Changed;

    public bool Has(StatusEffectData data) => Find(data) != null;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (indicator != null) indicator.enabled = false;
    }

    private void OnEnable()
    {
        health.Hit += OnHit;
        health.Died += ClearAll;
        health.Revived += ClearAll;
    }

    private void OnDisable()
    {
        health.Hit -= OnHit;
        health.Died -= ClearAll;
        health.Revived -= ClearAll;
        ClearAll();
    }

    private void Update()
    {
        if (active.Count == 0) return;

        float dt = Time.deltaTime;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            ActiveEffect effect = active[i];
            effect.elapsed += dt;
            effect.remaining -= dt;
            effect.tickTimer -= dt;

            while (effect.tickTimer <= 0f)
            {
                effect.tickTimer += effect.data.tickInterval;
                health.TakeDamage(effect.data.tickDamage);
                // 지속 피해로 쓰러지면 Died에서 모두 해제됨
                if (health.IsDead) return;
            }

            if (effect.remaining <= 0f)
            {
                active.RemoveAt(i);
                Changed?.Invoke();
            }
        }

        UpdateIndicator();
    }

    private void OnHit(AttackData attack)
    {
        if (attack.element == ElementType.None || health.IsDead || reactions == null) return;

        foreach (StatusEffectData reaction in reactions)
        {
            if (reaction != null && reaction.appliedBy == attack.element) Apply(reaction);
        }
    }

    public void Apply(StatusEffectData data)
    {
        if (data == null || health.IsDead) return;

        ActiveEffect effect = Find(data);
        if (effect != null)
        {
            effect.remaining = data.duration;
            return;
        }

        active.Add(new ActiveEffect { data = data, remaining = data.duration, tickTimer = data.tickInterval });
        UpdateIndicator();
        Changed?.Invoke();
    }

    private void ClearAll()
    {
        if (active.Count == 0) return;

        active.Clear();
        UpdateIndicator();
        Changed?.Invoke();
    }

    private ActiveEffect Find(StatusEffectData data)
    {
        foreach (ActiveEffect effect in active)
        {
            if (effect.data == data) return effect;
        }
        return null;
    }

    private void UpdateIndicator()
    {
        if (indicator == null) return;

        ActiveEffect shown = active.Count > 0 ? active[active.Count - 1] : null;
        Sprite[] frames = shown?.data.indicatorFrames;
        if (frames == null || frames.Length == 0)
        {
            indicator.enabled = false;
            return;
        }

        indicator.enabled = true;
        indicator.sprite = frames[Mathf.FloorToInt(shown.elapsed * shown.data.frameRate) % frames.Length];
    }
}
