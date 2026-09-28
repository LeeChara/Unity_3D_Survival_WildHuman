using UnityEngine;

// 피격 시 파츠 스프라이트를 잠깐 다른 색으로 물들임
// Health만 참조하므로 몬스터, 플레이어 등 Health를 가진 대상에 공용으로 사용
public class HitFlash : MonoBehaviour
{
    [SerializeField] private HitFlashSetting setting;
    [SerializeField] private Health health;
    [Tooltip("피격 시 색이 바뀌는 파츠 (아이콘 등 제외)")]
    [SerializeField] private SpriteRenderer[] targets;

    // 피격 색이 유지되고 남은 시간
    private float timer;

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;

        EndFlash();
    }

    private void Update()
    {
        if (timer <= 0f) return;

        timer -= Time.deltaTime;
        if (timer <= 0f) EndFlash();
    }

    // 반짝이는 중에 다시 맞으면 지속시간만 다시 채움
    private void OnDamaged()
    {
        if (health.IsDead) return;

        timer = setting.duration;
        SetColor(setting.color);
    }

    private void OnDied()
    {
        EndFlash();
    }

    private void EndFlash()
    {
        timer = 0f;
        SetColor(Color.white);
    }

    // SpriteRenderer.color는 원래 색에 곱해지므로 흰색이면 원래 색 그대로
    private void SetColor(Color color)
    {
        if (targets == null) return;

        foreach (SpriteRenderer target in targets)
        {
            if (target != null) target.color = color;
        }
    }
}
