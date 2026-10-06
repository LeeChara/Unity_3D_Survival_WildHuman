using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// 타이틀 화면 버튼 처리와 진입·퇴장 시 화면 페이드
public class MainMenuUI : MonoBehaviour
{
    [Tooltip("화면 전체를 덮는 검은 이미지의 CanvasGroup (페이드 중에는 레이캐스트를 막아 중복 클릭 방지)")]
    [SerializeField] private CanvasGroup fader;
    [SerializeField] private float fadeDuration = 0.6f;
    [Tooltip("시작 시 선택해 둘 버튼 (키보드 조작용, 선택된 버튼이 강조 표시됨)")]
    [SerializeField] private GameObject firstSelected;

    private bool isLeaving;

    private void Start()
    {
        StartCoroutine(Fade(1f, 0f));
        if (firstSelected != null) EventSystem.current.SetSelectedGameObject(firstSelected);
    }

    // 버튼 OnClick에 연결
    public void StartGame()
    {
        Leave(SceneFlow.LoadMain);
    }

    public void QuitGame()
    {
        Leave(SceneFlow.Quit);
    }

    private void Leave(Action onFaded)
    {
        if (isLeaving) return;
        isLeaving = true;
        StartCoroutine(FadeThen(onFaded));
    }

    private IEnumerator FadeThen(Action onFaded)
    {
        yield return Fade(0f, 1f);
        onFaded();
    }

    // 일시정지 메뉴에서 넘어와도 멈추지 않도록 실제 시간 기준으로 진행
    private IEnumerator Fade(float from, float to)
    {
        fader.blocksRaycasts = true;

        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            fader.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }

        fader.alpha = to;
        fader.blocksRaycasts = to > 0f;
    }
}
