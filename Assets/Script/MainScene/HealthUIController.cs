using UnityEngine;
using UnityEngine.UI;

public class HealthUIController : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Health targetHealth;

    private void Awake()
    {
        healthSlider.maxValue = targetHealth.MaxHealth;
        UpdateHealthUI();
    }
    void Update()
    {
        UpdateHealthUI();
    }

    private void UpdateHealthUI()
    {
        healthSlider.value = targetHealth.CurrentHealth;
    }
}