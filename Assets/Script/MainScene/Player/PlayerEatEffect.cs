using UnityEngine;

// 음식을 먹으면 파츠 스프라이트를 잠깐 회복 색으로 물들이고, 몸 주변에 반짝이 파티클을 띄움
// 몸 반짝임은 피격 반짝임(HitFlash)과 같은 파츠를 쓰므로, 피격되면 즉시 중단해 피격 색이 우선하도록 함
// 주변 반짝이는 파츠 색과 겹치지 않으므로 피격과 상관없이 끝까지 재생 (몸을 따라감)
public class PlayerEatEffect : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private HitFlashSetting setting;
    [SerializeField] private PlayerItemUser itemUser;
    [SerializeField] private Health health;
    [Tooltip("먹었을 때 색이 바뀌는 파츠 (HitFlash와 같은 파츠)")]
    [SerializeField] private SpriteRenderer[] targets;

    [Header("주변 반짝이")]
    [SerializeField] private EffectData sparkleEffect;

    // 회복 색이 유지되고 남은 시간
    private float timer;

    private void Awake()
    {
        if (itemUser == null) itemUser = GetComponent<PlayerItemUser>();
        if (health == null) health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        itemUser.ItemUsed += OnItemUsed;
        health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        itemUser.ItemUsed -= OnItemUsed;
        health.Damaged -= OnDamaged;

        if (timer > 0f) EndFlash();
    }

    private void Update()
    {
        UpdateFlash();
    }

    // 반짝이는 중에 다시 먹으면 지속시간만 다시 채우고, 반짝이는 새로 뿌림
    private void OnItemUsed(ItemData item)
    {
        if (item is not FoodItemData) return;

        timer = setting.healDuration;
        SetColor(setting.healColor);

        Effects.PlayOn(sparkleEffect, health.transform, null, true);
    }

    // 색 복구는 HitFlash가 하므로 타이머만 끔 (여기서 흰색으로 되돌리면 피격 색이 지워짐)
    private void OnDamaged()
    {
        timer = 0f;
    }

    private void UpdateFlash()
    {
        if (timer <= 0f) return;

        timer -= Time.deltaTime;
        if (timer <= 0f) EndFlash();
    }

    private void EndFlash()
    {
        timer = 0f;
        SetColor(Color.white);
    }

    // SpriteRenderer.color는 원래 색에 곱해지므로 흰색이면 원래 색 그대로
    private void SetColor(Color value)
    {
        if (targets == null) return;

        foreach (SpriteRenderer target in targets)
        {
            if (target != null) target.color = value;
        }
    }
}
