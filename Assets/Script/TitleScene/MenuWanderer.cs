using UnityEngine;

// 타이틀 화면에서 몬스터가 랜덤한 지점을 골라 걸어다님. 클릭에 놀라면 반대 방향으로 잠깐 빠르게 도망
// 부모 영역(화면 전체로 늘린 컨테이너)의 가운데를 anchor로 두고 사용
public class MenuWanderer : MonoBehaviour
{
    [Tooltip("이동 방향에 따라 좌우 반전·걸음 연출을 적용할 자식 (Visual의 부모)")]
    [SerializeField] private RectTransform body;
    [Tooltip("원본 스프라이트가 왼쪽을 바라보는지 여부")]
    [SerializeField] private bool spriteFacesLeft = true;
    [SerializeField] private float speed = 120f;
    [SerializeField] private float startleSpeedMultiplier = 2.5f;
    [SerializeField] private float startleDistance = 350f;
    [SerializeField] private Vector2 waitRange = new(0.5f, 2.5f);

    [Header("돌아다닐 범위 (부모 영역 가장자리로부터의 여백)")]
    [SerializeField] private float sideMargin = 120f;
    [SerializeField] private float bottomMargin = 20f;
    [Tooltip("몸 윗부분이 로고와 겹치지 않도록 비울 위쪽 여백 (자기 키만큼은 자동으로 더 비움)")]
    [SerializeField] private float topMargin = 500f;

    [Header("걸음·숨쉬기 연출")]
    [SerializeField] private float stepHeight = 9f;
    [SerializeField] private float stepSpeed = 10f;
    [SerializeField] private float breatheAmount = 0.03f;
    [SerializeField] private float breatheSpeed = 2.5f;
    [Tooltip("픽셀 아트가 어긋나 보이지 않도록 걸음 높이를 이 단위로 끊음")]
    [SerializeField] private float pixelStep = 3f;

    private RectTransform rect;
    private RectTransform area;
    private Vector2 target;
    private float waitTimer;
    private bool isStartled;
    private float phase;

    private void Awake()
    {
        rect = (RectTransform)transform;
        area = (RectTransform)rect.parent;
        // 여러 마리가 똑같이 움직이지 않도록 주기를 어긋나게 함
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Start()
    {
        BeginWait();
    }

    private void Update()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.unscaledDeltaTime;
            Animate(false);
            if (waitTimer <= 0f) target = RandomPoint();
            return;
        }

        Vector2 position = rect.anchoredPosition;
        Vector2 toTarget = target - position;
        float step = speed * (isStartled ? startleSpeedMultiplier : 1f) * Time.unscaledDeltaTime;

        if (toTarget.magnitude <= step)
        {
            rect.anchoredPosition = target;
            BeginWait();
            return;
        }

        rect.anchoredPosition = position + toTarget.normalized * step;
        Face(toTarget.x);
        Animate(true);
    }

    // 클릭한 화면 위치의 반대쪽으로 도망
    public void Startle(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPosition, null, out Vector2 clickPoint);
        Vector2 away = rect.anchoredPosition - clickPoint;
        if (away.sqrMagnitude < 1f) away = Random.insideUnitCircle;

        target = ClampToArea(rect.anchoredPosition + away.normalized * startleDistance);
        isStartled = true;
        waitTimer = 0f;
    }

    // 화면 좌우 바깥에서 시작해 안쪽 지점으로 걸어 들어옴
    public void EnterFromEdge()
    {
        float side = Random.value < 0.5f ? -1f : 1f;
        float x = side * (area.rect.width * 0.5f + rect.rect.width);
        Vector2 inside = RandomPoint();
        rect.anchoredPosition = new Vector2(x, inside.y);
        target = new Vector2(side * Mathf.Abs(inside.x) * 0.5f, inside.y);
        isStartled = false;
        waitTimer = 0f;
    }

    private void BeginWait()
    {
        isStartled = false;
        waitTimer = Random.Range(waitRange.x, waitRange.y);
    }

    private Vector2 RandomPoint()
    {
        Rect bounds = Bounds();
        return new Vector2(Random.Range(bounds.xMin, bounds.xMax), Random.Range(bounds.yMin, bounds.yMax));
    }

    private Vector2 ClampToArea(Vector2 point)
    {
        Rect bounds = Bounds();
        return new Vector2(Mathf.Clamp(point.x, bounds.xMin, bounds.xMax), Mathf.Clamp(point.y, bounds.yMin, bounds.yMax));
    }

    // pivot이 발(아래쪽 가운데)이므로 y는 발 위치 기준. 위쪽은 키만큼 더 내려서 머리가 여백을 넘지 않게 함
    private Rect Bounds()
    {
        Rect areaRect = area.rect;
        float yMin = areaRect.yMin + bottomMargin;
        return Rect.MinMaxRect(
            areaRect.xMin + sideMargin,
            yMin,
            areaRect.xMax - sideMargin,
            Mathf.Max(yMin, areaRect.yMax - topMargin - rect.rect.height));
    }

    private void Face(float directionX)
    {
        if (Mathf.Approximately(directionX, 0f)) return;

        bool movingLeft = directionX < 0f;
        float flip = movingLeft == spriteFacesLeft ? 1f : -1f;
        body.localScale = new Vector3(flip, body.localScale.y, 1f);
    }

    // 걷는 중: 통통 튀는 걸음, 멈춘 중: 숨쉬듯 세로로 늘었다 줄어듦
    private void Animate(bool moving)
    {
        float time = Time.unscaledTime;
        float flip = body.localScale.x;

        if (moving)
        {
            float speedScale = isStartled ? startleSpeedMultiplier : 1f;
            float hop = Mathf.Abs(Mathf.Sin(time * stepSpeed * speedScale + phase)) * stepHeight;
            if (pixelStep > 0f) hop = Mathf.Round(hop / pixelStep) * pixelStep;
            body.localPosition = new Vector3(0f, hop, 0f);
            body.localScale = new Vector3(flip, 1f, 1f);
        }
        else
        {
            body.localPosition = Vector3.zero;
            float breathe = 1f + Mathf.Sin(time * breatheSpeed + phase) * breatheAmount;
            body.localScale = new Vector3(flip, breathe, 1f);
        }
    }
}
