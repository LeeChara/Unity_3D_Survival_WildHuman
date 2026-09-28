using UnityEngine;

public class PlayerStatusUI : MonoBehaviour
{
    [SerializeField] private Health playerHealth;
    [SerializeField] private PlayerHunger playerHunger;

    [SerializeField] private StatBarUI healthBar;
    [SerializeField] private StatBarUI hungerBar;

    private void OnEnable()
    {
        playerHealth.HealthChanged += healthBar.Set;
        playerHunger.HungerChanged += hungerBar.Set;
    }

    private void OnDisable()
    {
        // 재활성화 시 중복 등록되지 않도록 OnEnable과 짝을 맞춰 해제
        playerHealth.HealthChanged -= healthBar.Set;
        playerHunger.HungerChanged -= hungerBar.Set;
    }

    private void Start()
    {
        // Awake에서 초기화된 값은 이벤트로 전달되지 않으므로 직접 한 번 반영
        healthBar.Set(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        hungerBar.Set(playerHunger.CurrentHunger, playerHunger.MaxHunger);
    }
}
