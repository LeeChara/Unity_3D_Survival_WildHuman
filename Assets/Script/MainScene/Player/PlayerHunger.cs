using System;
using UnityEngine;

public class PlayerHunger : MonoBehaviour
{
    [SerializeField] private float maxHunger = 100f;
    // 인스펙터에서 현재 허기 확인용 (Awake에서 maxHunger로 초기화)
    [SerializeField] private float currentHunger;

    // 허기 감소 속도·굶주림 데미지 규칙
    [SerializeField] private GameSetting gameSetting;

    private HungerRule Rule => gameSetting.hunger;

    private Health health;
    private PlayerMovement playerMovement;
    private float starveTimer;

    // 읽기 전용 프로퍼티
    public float MaxHunger => maxHunger;
    public float CurrentHunger => currentHunger;
    public bool IsStarving => currentHunger <= 0f;

    // 허기 변화 시점을 UI 등 외부 시스템에 알림 (현재값, 최대값)
    public event Action<float, float> HungerChanged;

    private void Awake()
    {
        health = GetComponent<Health>();
        playerMovement = GetComponent<PlayerMovement>();

        currentHunger = maxHunger;
    }

    private void Update()
    {
        if (health.IsDead) return;

        float decayPerSecond = maxHunger / Rule.timeToEmpty;
        if (playerMovement != null && playerMovement.IsSprinting) decayPerSecond *= Rule.sprintDecayMultiplier;

        SetHunger(currentHunger - decayPerSecond * Time.deltaTime);

        if (IsStarving)
        {
            starveTimer += Time.deltaTime;
            if (starveTimer >= Rule.starveInterval)
            {
                starveTimer -= Rule.starveInterval;
                health.TakeDamage(Rule.starveDamage);
            }
        }
        else
        {
            // 음식으로 회복하면 다음 굶주림 때 처음부터 다시 카운트
            starveTimer = 0f;
        }
    }

    public void Eat(float amount)
    {
        SetHunger(currentHunger + amount);
    }

    // 부활 시 허기를 가득 채움
    public void Refill()
    {
        starveTimer = 0f;
        SetHunger(maxHunger);
    }

    private void SetHunger(float value)
    {
        float newHunger = Mathf.Clamp(value, 0f, maxHunger);
        if (Mathf.Approximately(newHunger, currentHunger)) return;

        currentHunger = newHunger;
        HungerChanged?.Invoke(currentHunger, maxHunger);
    }
}
