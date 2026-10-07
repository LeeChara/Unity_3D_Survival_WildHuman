using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class Health : MonoBehaviour, ISaveable
{
    [SerializeField] private float maxHealth = 100;
    // 인스펙터에서 현재 체력 확인용 (Awake에서 maxHealth로 초기화)
    [SerializeField] private float currentHealth;
    private bool isDead;

    // 읽기 전용 프로퍼티
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;
    // 마우스를 올렸을 때 표시할 이름 (빈 문자열이면 표시하지 않음, 종류별로 데이터에서 읽도록 재정의)
    public virtual string DisplayName => string.Empty;

    // 사망 시점을 스포너 등 외부 시스템에 알림
    public event Action Died;
    // 체력 감소 시점을 이펙트 등 외부 시스템에 알림
    public event Action Damaged;
    // 체력 변화 시점을 UI 등 외부 시스템에 알림 (현재값, 최대값)
    public event Action<float, float> HealthChanged;
    // 부활 시점을 이펙트·UI 등 외부 시스템에 알림
    public event Action Revived;
    // 자식 클래스에서 재정의 시 반드시 base.Awake() 호출 (체력 초기화)
    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        isDead = false;
    }

    public void Init(float maxHealth)
    {
        this.maxHealth = maxHealth;
        currentHealth = this.maxHealth;
        isDead = false;
        HealthChanged?.Invoke(currentHealth, this.maxHealth);
    }

    // 공격 정보(도구 종류 등)에 따라 피해량을 보정한 뒤 적용
    public void TakeHit(AttackData attack)
    {
        TakeDamage(CalculateDamage(attack));
    }

    // 대상별 피해 보정이 필요하면 자식 클래스에서 재정의 (예: 자원의 도구 배율)
    protected virtual int CalculateDamage(AttackData attack)
    {
        return attack.damage;
    }

    public void TakeDamage(int damage)
    {
        // Destroy는 프레임 끝에 처리되므로 같은 프레임의 추가 피격으로 Die가 중복 호출되는 것을 방지
        if (isDead) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        Damaged?.Invoke();
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            isDead = true;
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;

        float newHealth = Mathf.Min(maxHealth, currentHealth + amount);
        if (Mathf.Approximately(newHealth, currentHealth)) return;

        currentHealth = newHealth;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    // 사망 상태를 해제하고 체력을 가득 채움
    public void Revive()
    {
        if (!isDead) return;

        isDead = false;
        currentHealth = maxHealth;
        HealthChanged?.Invoke(currentHealth, maxHealth);
        Revived?.Invoke();
    }

    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} has died.");
        Died?.Invoke();
    }

    #region 저장

    public string SaveKey => "health";

    public virtual JToken Save()
    {
        return new JObject { ["hp"] = currentHealth };
    }

    // 사망한 개체는 저장하지 않으므로 살아 있는 상태로만 복원
    public void Load(JToken data)
    {
        currentHealth = Mathf.Clamp(data.Value<float>("hp"), 1f, maxHealth);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    #endregion
}
