using System;
using UnityEngine;

// 사망 시 시체를 일정 시간 유지한 뒤 제거
// 행동 정지는 MonsterAI, 붉은색 유지는 HitFlash가 Died 이벤트를 받아 각자 처리
// 시체는 콜라이더가 남아 있어 피격 시 넉백만 적용됨 (데미지는 TakeDamage에서 무시)
public class MonsterHealth : Health
{
    [SerializeField] private MonsterEffectSetting setting;

    // 시체가 사라지는 시점을 아이템 드랍 등 외부 시스템에 알림 (Destroy 직전 호출)
    public event Action Vanished;

    private Rigidbody rb;
    private Knockback knockback;

    // 시체가 남아 있고 남은 시간
    private float corpseTimer;
    private bool isCorpse;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
        knockback = GetComponent<Knockback>();
    }

    private void OnEnable()
    {
        if (knockback != null) knockback.Ended += OnKnockbackEnded;
    }

    private void OnDisable()
    {
        if (knockback != null) knockback.Ended -= OnKnockbackEnded;
    }

    private void Update()
    {
        if (!isCorpse) return;

        corpseTimer -= Time.deltaTime;
        if (corpseTimer <= 0f) Vanish();
    }

    protected override void Die()
    {
        base.Die();

        isCorpse = true;
        corpseTimer = setting.corpseDuration;
    }

    // Knockback은 종료 시 속도를 원래 이동 로직에 넘기는데,
    // 시체는 이동 로직이 없으므로 마지막 속도로 계속 미끄러지지 않도록 수평 속도 제거
    private void OnKnockbackEnded()
    {
        if (!isCorpse) return;

        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(0f, velocity.y, 0f);
    }

    private void Vanish()
    {
        isCorpse = false;

        SpawnDeathEffect();
        Vanished?.Invoke();

        Destroy(gameObject);
    }

    // 이펙트는 시체와 함께 파괴되지 않도록 부모 없이 생성 (이펙트 자체 제거는 이펙트 프리팹이 담당)
    private void SpawnDeathEffect()
    {
        if (setting.deathEffectPrefab == null) return;

        Instantiate(setting.deathEffectPrefab, transform.position, Quaternion.identity);
    }
}
