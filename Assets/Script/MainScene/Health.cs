using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100;
    private float currentHealth;

    // 읽기 전용 프로퍼티
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public void Awake()
    {
        currentHealth = maxHealth;
    }

    public void Init(float maxHealth)
    {
        this.maxHealth = maxHealth;
        currentHealth = this.maxHealth;
    }

    public void TakeDamage(int damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} has died.");
    }
}
