using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 불타는 이펙트: 대상의 그림(스프라이트) 전체를 덮도록 불꽃 프레임을 겹쳐 그리고, 작은 광원을 켬
// - 지속형: Combustible이 대상의 자식으로 한 번 만들고 타는 동안만 켬 (Show/Hide)
// - 순간형: 바닥 아이템이 탈 때처럼 잠깐 터졌다 사라짐 (PlayBurst)
// 불꽃 한 개는 대상 높이에 맞추고, 대상이 넓으면 여러 개를 나란히 둬서 가로를 덮음
public class BurnEffect : MonoBehaviour
{
    // 카메라 각도는 프로젝트 전체에서 45도로 고정 (AxisAlignedBillboard와 같은 전제)
    private const float CameraYAngle = 45f;
    // 부모 SortingGroup 안에서 다른 파츠보다 앞에 그려지도록 하는 순서
    private const int OrderInsideGroup = 500;

    [Header("불꽃")]
    [Tooltip("불꽃 한 개를 그릴 스프라이트 (복제해서 여러 개를 씀, 피벗은 발밑)")]
    [SerializeField] private SpriteRenderer flameTemplate;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(0.1f)] private float frameRate = 8f;
    [Tooltip("대상 그림 높이 대비 불꽃 높이")]
    [SerializeField, Min(0.1f)] private float heightScale = 1.05f;
    [Tooltip("대상 그림 너비 대비 불꽃이 덮는 너비")]
    [SerializeField, Min(0.1f)] private float widthScale = 1.1f;
    [Tooltip("불꽃 최소 높이 (m, 아주 작은 대상도 보이도록)")]
    [SerializeField, Min(0.1f)] private float minHeight = 0.8f;
    [Tooltip("불꽃 그림 너비 중 실제로 대상을 가리는 몸통 비율 (가장자리 불티는 가리지 못하므로 이만큼만 덮는다고 보고 개수를 정함)")]
    [SerializeField, Range(0.2f, 1f)] private float coreWidth = 0.6f;
    [SerializeField, Range(1, 8)] private int maxFlames = 5;
    [Tooltip("불꽃마다 크기를 흔드는 정도 (일렁임)")]
    [SerializeField, Range(0f, 0.3f)] private float sway = 0.08f;

    [Header("빛")]
    [SerializeField] private LightSource fireLight;
    [Tooltip("광원 반경 = 덮는 너비 × 이 값 (최소 minLightRadius)")]
    [SerializeField, Min(0f)] private float lightRadiusPerWidth = 1.5f;
    [SerializeField, Min(0f)] private float minLightRadius = 2.5f;
    [SerializeField, Min(0f)] private float maxLightRadius = 6f;

    [Header("순간형")]
    [Tooltip("PlayBurst로 만들었을 때 사라지기까지의 시간 (초)")]
    [SerializeField, Min(0.05f)] private float burstDuration = 0.6f;

    private readonly List<SpriteRenderer> flames = new();
    private readonly List<float> phases = new();
    private SortingGroup sortingGroup;
    private float elapsed;
    private float burstLeft = -1f;
    private float baseLightRadius;
    private Color baseColor;

    private void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
        baseColor = flameTemplate.color;
        flameTemplate.gameObject.SetActive(false);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        for (int i = 0; i < flames.Count; i++)
        {
            SpriteRenderer flame = flames[i];
            if (!flame.gameObject.activeSelf) continue;

            float t = elapsed + phases[i];
            if (frames != null && frames.Length > 0) flame.sprite = frames[Mathf.FloorToInt(t * frameRate) % frames.Length];
        }

        if (burstLeft < 0f) return;

        burstLeft -= Time.deltaTime;
        float alpha = Mathf.Clamp01(burstLeft / burstDuration);
        foreach (SpriteRenderer flame in flames) flame.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
        if (fireLight != null) fireLight.Configure(baseLightRadius * alpha, 1.5f, fireLight.Color, 0.15f);
        if (burstLeft <= 0f) Destroy(gameObject);
    }

    // 대상의 자식으로 붙어 대상 그림을 덮는 지속형 이펙트를 만듦 (처음에는 꺼진 상태)
    public static BurnEffect Attach(BurnEffect prefab, Transform target)
    {
        BurnEffect effect = Instantiate(prefab, target);
        effect.gameObject.SetActive(false);
        return effect;
    }

    // 잠깐 터졌다 사라지는 순간형 이펙트 (예: 바닥 아이템이 탐)
    public static void PlayBurst(BurnEffect prefab, Bounds cover)
    {
        if (prefab == null) return;

        BurnEffect effect = Instantiate(prefab);
        effect.gameObject.SetActive(true);
        effect.Fit(cover, null);
        effect.burstLeft = effect.burstDuration;
    }

    // 켤 때마다 대상 그림 크기를 다시 재서 맞춤 (자라는 대상, 풀에서 재사용되는 대상)
    public void Show(Transform target)
    {
        gameObject.SetActive(true);
        if (TryGetCover(target, out Bounds cover)) Fit(cover, target);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Fit(Bounds cover, Transform target)
    {
        // 화면 가로(카메라 오른쪽, 수평 45도) 방향으로 본 너비. 월드 축 상자를 그 축에 투영한 길이
        float width = (cover.size.x + cover.size.z) * Mathf.Sin(CameraYAngle * Mathf.Deg2Rad) * widthScale;
        float height = Mathf.Max(cover.size.y * heightScale, minHeight);

        transform.SetPositionAndRotation(new Vector3(cover.center.x, cover.min.y, cover.center.z), Quaternion.Euler(0f, CameraYAngle, 0f));
        transform.localScale = Vector3.one;
        if (transform.parent != null)
        {
            // 부모 배율을 상쇄해서 월드 기준 크기로 맞춤
            Vector3 parentScale = transform.parent.lossyScale;
            transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
        }

        // 불꽃 한 개에서 실제로 보이는 부분의 크기·위치 (그림 안의 투명 여백 제외)
        Sprite sprite = frames != null && frames.Length > 0 ? frames[0] : flameTemplate.sprite;
        Rect visible = VisibleRect(sprite);
        Vector2 spriteSize = visible.size;
        float flameScale = height / Mathf.Max(spriteSize.y, 0.01f);
        float flameWidth = spriteSize.x * flameScale;

        float coverPerFlame = Mathf.Max(flameWidth * coreWidth, 0.01f);
        int count = Mathf.Clamp(Mathf.CeilToInt(width / coverPerFlame), 1, maxFlames);
        // 개수 제한으로 덮지 못하면 불꽃을 가로로 늘려서 덮음
        float scaleX = Mathf.Max(flameScale, width / count / coreWidth / Mathf.Max(spriteSize.x, 0.01f));
        // 바깥 불꽃의 몸통이 대상 가장자리에 오도록 몸통 너비 기준으로 나란히 배치
        float core = spriteSize.x * scaleX * coreWidth;
        float spacing = count > 1 ? (width - core) / (count - 1) : 0f;
        // 보이는 부분의 가로 중심·아래쪽이 기준점에 오도록 보정
        float pivotX = visible.center.x;
        float pivotY = visible.yMin;

        EnsureFlames(count);
        for (int i = 0; i < flames.Count; i++)
        {
            SpriteRenderer flame = flames[i];
            bool used = i < count;
            flame.gameObject.SetActive(used);
            if (!used) continue;

            // 가운데 불꽃이 가장 크고 바깥쪽으로 조금씩 작게, 앞뒤로 살짝 엇갈려 겹침이 자연스럽게
            float fromCenter = count > 1 ? Mathf.Abs(i - (count - 1) * 0.5f) / ((count - 1) * 0.5f) : 0f;
            float size = 1f - fromCenter * 0.2f + Random.Range(-sway, sway);
            float x = count > 1 ? -width * 0.5f + core * 0.5f + spacing * i : 0f;
            float sx = scaleX * size;
            float sy = flameScale * size;
            flame.transform.localPosition = new Vector3(x - pivotX * sx, -pivotY * sy, (i % 2 == 0 ? -0.02f : 0.02f));
            flame.transform.localScale = new Vector3(sx, sy, 1f);
            flame.sortingOrder = i % 2;
            flame.color = baseColor;
        }

        if (fireLight != null)
        {
            baseLightRadius = Mathf.Clamp(width * lightRadiusPerWidth, minLightRadius, maxLightRadius);
            fireLight.Configure(baseLightRadius, 2f, fireLight.Color, 0.15f);
        }

        UpdateSorting(target, cover);
    }

    // 스프라이트 메시(투명 부분을 잘라낸 외곽선)의 범위. 피벗 기준 로컬 단위
    private static Rect VisibleRect(Sprite sprite)
    {
        if (sprite == null) return new Rect(-0.5f, 0f, 1f, 1f);

        Vector2[] vertices = sprite.vertices;
        if (vertices == null || vertices.Length == 0)
        {
            Bounds b = sprite.bounds;
            return Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
        }

        Vector2 min = vertices[0];
        Vector2 max = vertices[0];
        foreach (Vector2 v in vertices)
        {
            min = Vector2.Min(min, v);
            max = Vector2.Max(max, v);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void EnsureFlames(int count)
    {
        while (flames.Count < count)
        {
            SpriteRenderer flame = Instantiate(flameTemplate, transform);
            flame.gameObject.SetActive(true);
            flames.Add(flame);
            phases.Add(Random.value * 10f);
        }
    }

    // 대상이 SortingGroup 안이면 그 안에서 가장 앞, 아니면(울타리처럼 파츠마다 그룹, 바닥 아이템) 깊이로 계산해 한 칸 앞
    private void UpdateSorting(Transform target, Bounds cover)
    {
        if (sortingGroup == null) return;

        SortingGroup parentGroup = transform.parent != null ? transform.parent.GetComponentInParent<SortingGroup>() : null;
        if (parentGroup != null)
        {
            sortingGroup.sortingOrder = OrderInsideGroup;
            return;
        }

        int order = DepthSortManager.Instance != null ? DepthSortManager.Instance.GetSortingOrder(cover.center) : 0;
        if (target != null)
        {
            foreach (SortingGroup part in target.GetComponentsInChildren<SortingGroup>())
            {
                if (part != sortingGroup) order = Mathf.Max(order, part.sortingOrder);
            }
        }
        sortingGroup.sortingOrder = order + 1;
    }

    // 대상의 보이는 그림 범위 (그림자·상태 아이콘·이 이펙트 자신은 제외)
    private bool TryGetCover(Transform target, out Bounds cover)
    {
        cover = default;
        bool any = false;
        foreach (SpriteRenderer part in target.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!part.enabled || part.sprite == null || part.transform.IsChildOf(transform)) continue;
            if (part.sortingLayerName == "Shadow" || part.sortingOrder >= 100) continue;

            // 투명 여백을 뺀 실제 그림 외곽선의 네 모서리를 월드 좌표로 옮겨 범위를 잡음
            Rect visible = VisibleRect(part.sprite);
            Matrix4x4 toWorld = part.transform.localToWorldMatrix;
            foreach (Vector2 corner in new[] { visible.min, visible.max, new Vector2(visible.xMin, visible.yMax), new Vector2(visible.xMax, visible.yMin) })
            {
                Vector3 point = toWorld.MultiplyPoint3x4(new Vector3(corner.x, corner.y, 0f));
                if (any) cover.Encapsulate(point);
                else cover = new Bounds(point, Vector3.zero);
                any = true;
            }
        }
        return any;
    }
}
