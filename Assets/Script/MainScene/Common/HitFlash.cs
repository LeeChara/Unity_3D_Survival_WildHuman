using UnityEngine;

// 피격 시 파츠 스프라이트를 잠깐 다른 색으로 물들임
// Health만 참조하므로 몬스터, 플레이어, 자원 등 Health를 가진 대상에 공용으로 사용
public class HitFlash : MonoBehaviour
{
    // 대상 종류에 따라 HitFlashSetting의 어떤 색을 쓸지 결정
    public enum HitFlashType { Character, Resource }

    [SerializeField] private HitFlashSetting setting;
    [SerializeField] private HitFlashType type = HitFlashType.Character;
    [SerializeField] private Health health;
    [Tooltip("피격 시 색이 바뀌는 파츠 (아이콘 등 제외)")]
    [SerializeField] private SpriteRenderer[] targets;

    // 피격 색이 유지되고 남은 시간
    private float timer;
    // 반짝임이 끝나면 돌아갈 색 (보통 흰색, 불에 그을린 나무처럼 다른 컴포넌트가 바꿀 수 있음)
    private Color baseColor = Color.white;

    private Color FlashColor => type == HitFlashType.Resource ? setting.resourceColor : setting.color;

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
        health.Revived += EndFlash;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
        health.Revived -= EndFlash;

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
        SetColor(FlashColor);
    }

    // 사망 시에는 피격 색을 복구하지 않고 유지 (시체 연출)
    private void OnDied()
    {
        timer = 0f;
        SetColor(FlashColor);
    }

    private void EndFlash()
    {
        timer = 0f;
        SetColor(baseColor);
    }

    // 평소 색을 바꿈 (반짝이는 중이면 반짝임이 끝난 뒤 적용)
    public void SetBaseColor(Color color)
    {
        baseColor = color;
        if (timer <= 0f && !health.IsDead) SetColor(color);
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
