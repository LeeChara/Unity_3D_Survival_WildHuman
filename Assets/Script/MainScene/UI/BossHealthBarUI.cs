using TMPro;
using UnityEngine;

// 화면 위쪽 보스 체력바
// 플레이어와 싸우기 시작한 보스(BossAI.Engaged)를 표시하고, 싸움이 끝나거나 보스가 쓰러지면 잠시 뒤 사라짐
// 체력이 줄면 줄어든 만큼이 밝은 잔상으로 남았다가 따라 내려감. 2페이즈 경계에 눈금 표시
public class BossHealthBarUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [Tooltip("이름 글자 (외곽선용 복사본 포함, 모두 같은 글자로 채움)")]
    [SerializeField] private TMP_Text[] nameTexts;
    [Tooltip("현재 체력 (가로 앵커를 비율로 조절)")]
    [SerializeField] private RectTransform fill;
    [Tooltip("줄어든 체력의 잔상")]
    [SerializeField] private RectTransform trail;
    [Tooltip("2페이즈 경계 눈금")]
    [SerializeField] private RectTransform phaseMarker;

    [Header("연출")]
    [Tooltip("맞은 뒤 잔상이 줄어들기 시작할 때까지 (초)")]
    [SerializeField, Min(0f)] private float trailDelay = 0.4f;
    [Tooltip("잔상이 줄어드는 속도 (초당 체력 비율)")]
    [SerializeField, Min(0.01f)] private float trailSpeed = 0.6f;
    [Tooltip("나타나고 사라지는 시간 (초)")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.3f;
    [Tooltip("보스가 쓰러지거나 싸움이 끝난 뒤 사라지기까지 (초)")]
    [SerializeField, Min(0f)] private float hideDelay = 1.5f;

    private BossAI boss;
    private float ratio;
    private float trailRatio;
    private float trailHold;
    private bool visible;
    private float hideTimer;

    private void Awake()
    {
        group.alpha = 0f;
    }

    private void OnEnable()
    {
        BossAI.Engaged += OnEngaged;
        BossAI.Disengaged += OnDisengaged;
    }

    private void OnDisable()
    {
        BossAI.Engaged -= OnEngaged;
        BossAI.Disengaged -= OnDisengaged;
        Unbind();
    }

    private void OnEngaged(BossAI engaged)
    {
        if (boss != engaged)
        {
            Unbind();
            boss = engaged;
            boss.Health.HealthChanged += OnHealthChanged;

            string bossName = boss.Data.displayName;
            foreach (TMP_Text text in nameTexts) text.text = bossName;

            ratio = trailRatio = HealthRatio();
            trailHold = 0f;
            if (phaseMarker != null)
            {
                float marker = boss.Boss.phase2HealthRatio;
                phaseMarker.anchorMin = new Vector2(marker, phaseMarker.anchorMin.y);
                phaseMarker.anchorMax = new Vector2(marker, phaseMarker.anchorMax.y);
                phaseMarker.gameObject.SetActive(marker > 0f && marker < 1f);
            }
            ApplyBars();
        }

        visible = true;
        hideTimer = 0f;
    }

    // 쓰러진 경우에도 빈 체력바를 잠시 보여준 뒤 사라짐
    private void OnDisengaged(BossAI disengaged)
    {
        if (disengaged != boss) return;
        hideTimer = hideDelay;
    }

    private void Unbind()
    {
        if (boss != null) boss.Health.HealthChanged -= OnHealthChanged;
        boss = null;
        visible = false;
    }

    private void OnHealthChanged(float current, float max)
    {
        float newRatio = HealthRatio();
        // 회복하면 잔상 없이 바로 채움
        if (newRatio >= trailRatio) trailRatio = newRatio;
        else trailHold = trailDelay;
        ratio = newRatio;
        ApplyBars();
    }

    private float HealthRatio()
    {
        Health health = boss.Health;
        return health.MaxHealth > 0f ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 0f;
    }

    private void Update()
    {
        if (hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0f) visible = false;
        }
        // 보스가 사라졌으면 (청크 언로드 등) 바로 숨김
        if (boss == null && visible) visible = false;

        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.deltaTime / fadeDuration);
        if (!visible && group.alpha <= 0f && boss != null) Unbind();

        if (trailRatio > ratio)
        {
            if (trailHold > 0f) trailHold -= Time.deltaTime;
            else trailRatio = Mathf.MoveTowards(trailRatio, ratio, trailSpeed * Time.deltaTime);
            ApplyBars();
        }
    }

    private void ApplyBars()
    {
        fill.anchorMax = new Vector2(ratio, fill.anchorMax.y);
        trail.anchorMax = new Vector2(trailRatio, trail.anchorMax.y);
    }
}
