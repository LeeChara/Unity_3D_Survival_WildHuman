using UnityEngine;

// 저장 중 화면 구석에 표시 (WorldSaveManager의 저장 시작·완료에 맞춰 페이드)
// 일시정지 중에도 동작하도록 실제 시간 기준으로 진행
public class SaveIndicatorUI : MonoBehaviour
{
    [SerializeField] private WorldSaveManager saveManager;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private float fadeDuration = 0.3f;
    [Tooltip("저장이 금방 끝나도 깜빡이지 않도록 최소한 보여 줄 시간(초)")]
    [SerializeField] private float minVisibleTime = 1f;

    private bool isSaving;
    // 시작하자마자 최소 표시 시간에 걸려 보이지 않도록 아주 과거로 초기화
    private float shownAt = float.NegativeInfinity;

    private void Awake()
    {
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        saveManager.SaveStarted += OnSaveStarted;
        saveManager.SaveFinished += OnSaveFinished;
    }

    private void OnDisable()
    {
        saveManager.SaveStarted -= OnSaveStarted;
        saveManager.SaveFinished -= OnSaveFinished;
    }

    private void Update()
    {
        bool visible = isSaving || Time.unscaledTime - shownAt < minVisibleTime;
        float target = visible ? 1f : 0f;
        float step = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, step);
    }

    private void OnSaveStarted()
    {
        isSaving = true;
        shownAt = Time.unscaledTime;
    }

    private void OnSaveFinished()
    {
        isSaving = false;
    }
}
