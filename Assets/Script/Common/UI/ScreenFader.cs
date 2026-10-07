using System;
using System.Collections;
using UnityEngine;

// 화면 전체를 덮는 검은 이미지의 페이드 (씬 진입·퇴장)
// 페이드 중에는 레이캐스트를 막아 중복 클릭을 방지하고, 일시정지 메뉴에서 넘어와도 멈추지 않도록 실제 시간 기준으로 진행
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    [SerializeField] private float duration = 0.6f;

    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
    }

    // 검은 화면에서 밝아짐
    public void FadeIn()
    {
        StopAllCoroutines();
        StartCoroutine(Fade(1f, 0f));
    }

    // 어두워진 뒤 onFaded 실행 (씬 이동 등)
    public void FadeOutThen(Action onFaded)
    {
        StopAllCoroutines();
        StartCoroutine(FadeOutRoutine(onFaded));
    }

    private IEnumerator FadeOutRoutine(Action onFaded)
    {
        yield return Fade(group.alpha, 1f);
        onFaded?.Invoke();
    }

    private IEnumerator Fade(float from, float to)
    {
        group.blocksRaycasts = true;

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }

        group.alpha = to;
        group.blocksRaycasts = to > 0f;
    }
}
