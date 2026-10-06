using UnityEngine;

// 타이틀 화면을 가로질러 활공하는 장식. 화면 밖으로 나가면 잠시 쉬었다가 반대편에서 다시 등장
// 부모 영역의 가운데를 기준(anchor)으로 배치해서 사용
public class MenuGlider : MonoBehaviour
{
    [Tooltip("초당 이동 거리. 음수면 오른쪽에서 왼쪽으로 이동")]
    [SerializeField] private float speed = -260f;
    [SerializeField] private float waveHeight = 24f;
    [SerializeField] private float waveSpeed = 2.5f;
    [Tooltip("다시 등장하기까지 대기 시간 범위(초)")]
    [SerializeField] private Vector2 delayRange = new(3f, 7f);
    [SerializeField] private float firstDelay = 1.5f;

    private RectTransform rect;
    private RectTransform area;
    private float baseY;
    private float x;
    private float waitTimer;

    private void Awake()
    {
        rect = (RectTransform)transform;
        area = (RectTransform)rect.parent;
        baseY = rect.anchoredPosition.y;
    }

    private void Start()
    {
        Restart(firstDelay);
    }

    private void Update()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.unscaledDeltaTime;
            return;
        }

        x += speed * Time.unscaledDeltaTime;
        float y = baseY + Mathf.Sin(Time.unscaledTime * waveSpeed) * waveHeight;
        rect.anchoredPosition = new Vector2(x, y);

        if (Mathf.Abs(x) > EdgeX) Restart(Random.Range(delayRange.x, delayRange.y));
    }

    // 자기 크기만큼 더 나간 위치 = 완전히 화면 밖
    private float EdgeX => area.rect.width * 0.5f + rect.rect.width * Mathf.Abs(rect.localScale.x);

    // 출발 위치(화면 밖)로 돌아가 delay 후 다시 활공 (파괴 후 되살아날 때도 사용)
    public void Restart(float delay)
    {
        x = -Mathf.Sign(speed) * EdgeX;
        rect.anchoredPosition = new Vector2(x, baseY);
        waitTimer = delay;
    }
}
