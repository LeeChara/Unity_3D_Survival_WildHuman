using UnityEngine;

// 몬스터 상태에 따른 이펙트 표시
public class MonsterStatusEffect : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private MonsterAI monsterAI;
    [SerializeField] private Health health;
    [SerializeField] private SpriteRenderer emoteRenderer;

    [Header("말풍선")]
    [SerializeField] private Sprite exclamationSprite;
    [SerializeField] private Sprite questionSprite;
    [SerializeField] private float emoteDuration = 1f;

    [Header("땀방울")]
    [Tooltip("표시할 스프라이트는 이 SpriteRenderer에 직접 지정")]
    [SerializeField] private SpriteRenderer sweatRenderer;

    // 말풍선이 표시되고 남은 시간
    private float emoteTimer;

    private void Awake()
    {
        if (monsterAI == null) monsterAI = GetComponent<MonsterAI>();
        if (health == null) health = GetComponent<Health>();

        if (emoteRenderer != null) emoteRenderer.enabled = false;
        if (sweatRenderer != null) sweatRenderer.enabled = false;
    }

    private void OnEnable()
    {
        monsterAI.PlayerDetected += OnPlayerDetected;
        monsterAI.PlayerLost += OnPlayerLost;
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        monsterAI.PlayerDetected -= OnPlayerDetected;
        monsterAI.PlayerLost -= OnPlayerLost;
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;

        HideEmote();
        SetSweatVisible(false);
    }

    private void Update()
    {
        if (emoteTimer <= 0f) return;

        emoteTimer -= Time.deltaTime;
        if (emoteTimer <= 0f) HideEmote();
    }

    private void OnPlayerDetected()
    {
        ShowEmote(exclamationSprite);
    }

    private void OnPlayerLost()
    {
        ShowEmote(questionSprite);
    }

    // 새 말풍선이 오면 이전 말풍선을 즉시 교체
    private void ShowEmote(Sprite sprite)
    {
        if (emoteRenderer == null || sprite == null) return;
        if (health.IsDead) return;

        emoteRenderer.sprite = sprite;
        emoteRenderer.enabled = true;
        emoteTimer = emoteDuration;
    }

    private void HideEmote()
    {
        emoteTimer = 0f;
        if (emoteRenderer != null) emoteRenderer.enabled = false;
    }

    // 체력이 감소할 때마다 저체력 여부를 확인하고, 해당하면 땀방울을 계속 켜둠
    private void OnDamaged()
    {
        if (health.IsDead || health.MaxHealth <= 0f) return;

        float ratio = health.CurrentHealth / health.MaxHealth;
        SetSweatVisible(ratio <= monsterAI.Data.lowHealthRatio);
    }

    private void SetSweatVisible(bool visible)
    {
        if (sweatRenderer != null) sweatRenderer.enabled = visible;
    }

    private void OnDied()
    {
        HideEmote();
        SetSweatVisible(false);
    }
}
