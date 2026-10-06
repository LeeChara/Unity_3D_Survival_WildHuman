using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum MenuReaction
{
    Sway,   // 밑동을 축으로 좌우로 흔들림 (나무·덤불)
    Shake,  // 제자리에서 덜덜 떨림 (바위)
    Squash, // 찌그러졌다 펴짐 (몬스터)
}

// 타이틀 화면에서 클릭하면 반응하고, 여러 번 클릭하면 파괴됐다가 잠시 후 되살아나는 장식
// 구조: 이 오브젝트(위치·클릭 판정) → (몬스터는 Body: 이동 연출) → Visual(이미지, 반응 연출)
// Visual의 pivot은 아래쪽 가운데여야 흔들림·찌그러짐이 발 기준으로 일어남
public class MenuInteractable : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RectTransform visual;
    [SerializeField] private Graphic graphic;
    [Tooltip("파괴 중·숨김 상태에서 투명도와 클릭 판정을 함께 끄기 위함")]
    [SerializeField] private CanvasGroup visualGroup;
    [SerializeField] private MenuReaction reaction = MenuReaction.Sway;
    [SerializeField, Min(1)] private int maxHits = 5;
    [SerializeField] private Vector2 respawnDelayRange = new(5f, 8f);

    [Header("반응 연출")]
    [SerializeField] private float reactDuration = 0.45f;
    [Tooltip("Sway: 회전 각도, Shake: 이동 픽셀, Squash: 늘어나는 비율")]
    [SerializeField] private float reactStrength = 8f;
    [SerializeField] private Color flashColor = new(1f, 0.45f, 0.45f, 1f);
    [SerializeField] private float flashDuration = 0.15f;
    [Tooltip("파괴 시 사라지기 전에 깜빡이는 횟수")]
    [SerializeField] private int blinkCount = 3;
    [Tooltip("깜빡임 한 번(꺼짐+켜짐)의 절반 길이(초)")]
    [SerializeField] private float blinkInterval = 0.07f;
    [SerializeField] private float respawnDuration = 0.4f;

    private int hits;
    private bool isBroken;
    private Coroutine reactRoutine;
    private Color baseColor;
    private MenuWanderer wanderer;
    private MenuGlider glider;

    private void Awake()
    {
        baseColor = graphic.color;
        wanderer = GetComponent<MenuWanderer>();
        glider = GetComponent<MenuGlider>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isBroken || eventData.button != PointerEventData.InputButton.Left) return;

        hits++;
        if (hits >= maxHits)
        {
            Restart(BreakRoutine());
            return;
        }

        Restart(ReactRoutine());
        if (wanderer != null) wanderer.Startle(eventData.position);
    }

    private void Restart(IEnumerator routine)
    {
        if (reactRoutine != null) StopCoroutine(reactRoutine);
        ResetVisual();
        reactRoutine = StartCoroutine(routine);
    }

    private void ResetVisual()
    {
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;
        graphic.color = baseColor;
    }

    // 타이틀은 일시정지 메뉴에서 넘어올 수 있으므로 모든 연출은 실제 시간 기준
    private IEnumerator ReactRoutine()
    {
        for (float t = 0f; t < reactDuration; t += Time.unscaledDeltaTime)
        {
            float progress = t / reactDuration;
            float decay = 1f - progress;
            // 감쇠하며 3번 왕복
            float wave = Mathf.Sin(progress * Mathf.PI * 6f) * decay;

            switch (reaction)
            {
                case MenuReaction.Sway:
                    visual.localRotation = Quaternion.Euler(0f, 0f, wave * reactStrength);
                    break;
                case MenuReaction.Shake:
                    // 픽셀 아트가 번지지 않도록 정수 픽셀 단위로 이동
                    visual.localPosition = new Vector3(Mathf.Round(wave * reactStrength), 0f, 0f);
                    break;
                case MenuReaction.Squash:
                    visual.localScale = new Vector3(1f + wave * reactStrength, 1f - wave * reactStrength, 1f);
                    break;
            }

            graphic.color = t < flashDuration ? flashColor : baseColor;
            yield return null;
        }

        ResetVisual();
        reactRoutine = null;
    }

    private IEnumerator BreakRoutine()
    {
        isBroken = true;
        visualGroup.blocksRaycasts = false;
        // 깜빡이는 동안 제자리에 멈춰 있도록 이동 중지
        if (wanderer != null) wanderer.enabled = false;

        // 고전 게임처럼 붉게 몇 번 깜빡이다가 사라짐
        graphic.color = flashColor;
        var interval = new WaitForSecondsRealtime(blinkInterval);
        for (int i = 0; i < blinkCount; i++)
        {
            visualGroup.alpha = 0f;
            yield return interval;
            visualGroup.alpha = 1f;
            yield return interval;
        }

        visualGroup.alpha = 0f;
        ResetVisual();

        yield return new WaitForSecondsRealtime(Random.Range(respawnDelayRange.x, respawnDelayRange.y));

        yield return RespawnRoutine();
    }

    private IEnumerator RespawnRoutine()
    {
        hits = 0;

        if (wanderer != null)
        {
            // 몬스터는 화면 가장자리에서 걸어 들어옴
            wanderer.enabled = true;
            wanderer.EnterFromEdge();
            visualGroup.alpha = 1f;
        }
        else if (glider != null)
        {
            // 활공하는 장식은 다음 활공 때 다시 등장
            glider.Restart(0f);
            visualGroup.alpha = 1f;
        }
        else
        {
            // 제자리에서 다시 자라남 (살짝 커졌다가 원래 크기로)
            for (float t = 0f; t < respawnDuration; t += Time.unscaledDeltaTime)
            {
                float p = t / respawnDuration;
                float scale = p < 0.7f ? Mathf.Lerp(0f, 1.1f, p / 0.7f) : Mathf.Lerp(1.1f, 1f, (p - 0.7f) / 0.3f);
                visual.localScale = new Vector3(scale, scale, 1f);
                visualGroup.alpha = Mathf.Clamp01(p * 3f);
                yield return null;
            }
            visualGroup.alpha = 1f;
        }

        ResetVisual();
        visualGroup.blocksRaycasts = true;
        isBroken = false;
        reactRoutine = null;
    }
}
