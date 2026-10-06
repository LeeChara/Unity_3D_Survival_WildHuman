using UnityEngine;

// 타이틀 화면 장식용 제자리 움직임 (위아래 흔들림 + 숨쉬듯 세로로 늘었다 줄어듦)
// 발이 바닥에 붙어 보이도록 pivot을 아래쪽 가운데에 두고 사용
public class MenuIdleMotion : MonoBehaviour
{
    [SerializeField] private float bobHeight = 0f;
    [SerializeField] private float breatheAmount = 0.03f;
    [SerializeField] private float speed = 2f;
    [Tooltip("여러 개가 똑같이 움직이지 않도록 주기를 어긋나게 함 (라디안)")]
    [SerializeField] private float phase;
    [Tooltip("픽셀 아트가 어긋나 보이지 않도록 위치를 이 단위로 끊어서 이동 (0이면 부드럽게)")]
    [SerializeField] private float pixelStep = 3f;

    private RectTransform rect;
    private Vector2 basePosition;
    private Vector3 baseScale;

    private void Awake()
    {
        rect = (RectTransform)transform;
        basePosition = rect.anchoredPosition;
        baseScale = rect.localScale;
    }

    private void Update()
    {
        float wave = Mathf.Sin(Time.unscaledTime * speed + phase);

        float offset = wave * bobHeight;
        if (pixelStep > 0f) offset = Mathf.Round(offset / pixelStep) * pixelStep;
        rect.anchoredPosition = basePosition + new Vector2(0f, offset);

        rect.localScale = new Vector3(baseScale.x, baseScale.y * (1f + wave * breatheAmount), baseScale.z);
    }
}
