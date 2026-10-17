using UnityEngine;

// 선인장 모양 조립: 하단 기둥(고정) + 중단 줄기 1~3층(왼팔·오른팔·팔 없음 중 무작위) + 상단(백년초 있음·없음)
// 위치를 시드로 정해서 같은 자리의 선인장은 항상 같은 모양 (세이브에는 위치만 있으면 됨)
// 자원은 풀에서 재사용되고 꺼낸 뒤에 위치가 정해지므로, 위치가 바뀌면 다시 조립 (TileLake와 같은 방식)
// 높이에 맞춰 몸체·가시 판정과 그림자 높이도 바꿈
// 프리팹의 기본 그림(도감용 전체 모습)은 조립하면서 기둥 그림으로 바뀜
public class CactusShape : MonoBehaviour
{
    // 스프라이트 PPU(100) 기준 1픽셀의 로컬 크기 (자원 프리팹은 scale이 10이라 월드에서는 0.1m)
    private const float LocalPerPixel = 0.01f;

    [Header("그림")]
    [SerializeField] private SpriteRenderer baseRenderer;
    [Tooltip("아래층부터 차례로 (최대 층수만큼)")]
    [SerializeField] private SpriteRenderer[] segmentRenderers;
    [SerializeField] private SpriteRenderer topRenderer;
    [SerializeField] private Sprite baseSprite;
    [Tooltip("중단 줄기 종류 (왼팔, 오른팔, 팔 없음). 같은 확률로 고름")]
    [SerializeField] private Sprite[] segmentSprites;
    [SerializeField] private Sprite topSprite;
    [SerializeField] private Sprite topFruitSprite;

    [Header("모양")]
    [Tooltip("중단 줄기 층수 범위")]
    [SerializeField] private Vector2Int segmentCount = new Vector2Int(1, 3);
    [Tooltip("상단에 백년초가 달릴 확률")]
    [SerializeField, Range(0f, 1f)] private float fruitChance = 0.4f;
    [Tooltip("하단 기둥 높이 (px)")]
    [SerializeField, Min(1)] private int basePixels = 12;
    [Tooltip("중단 줄기 한 층의 높이 (px, 팔 끝은 이보다 위로 솟아도 됨)")]
    [SerializeField, Min(1)] private int segmentPixels = 12;
    [Tooltip("상단 높이 (px)")]
    [SerializeField, Min(1)] private int topPixels = 8;

    [Header("판정")]
    [SerializeField] private CapsuleCollider body;
    [Tooltip("접촉 피해 트리거 (ThornHazard)")]
    [SerializeField] private BoxCollider thorns;
    [SerializeField] private ShadowCaster shadowCaster;

    private bool built;
    private Vector3 builtAt;

    private void OnEnable()
    {
        built = false;
    }

    private void Update()
    {
        Vector3 position = transform.position;
        if (!built || position != builtAt) Build(position);
    }

    private void Build(Vector3 position)
    {
        built = true;
        builtAt = position;

        // 세이브 위치는 소수 둘째 자리까지이므로 같은 선인장은 항상 같은 시드
        int seed = unchecked(Mathf.RoundToInt(position.x * 100f) * 73856093 ^ Mathf.RoundToInt(position.z * 100f) * 19349663);
        var rng = new System.Random(seed);

        int count = Mathf.Clamp(rng.Next(segmentCount.x, segmentCount.y + 1), 0, segmentRenderers.Length);
        baseRenderer.sprite = baseSprite;

        int pixels = basePixels;
        for (int i = 0; i < segmentRenderers.Length; i++)
        {
            SpriteRenderer segment = segmentRenderers[i];
            bool used = i < count;
            segment.gameObject.SetActive(used);
            if (!used) continue;

            segment.sprite = segmentSprites[rng.Next(segmentSprites.Length)];
            segment.transform.localPosition = new Vector3(0f, pixels * LocalPerPixel, 0f);
            pixels += segmentPixels;
        }

        topRenderer.sprite = rng.NextDouble() < fruitChance ? topFruitSprite : topSprite;
        topRenderer.transform.localPosition = new Vector3(0f, pixels * LocalPerPixel, 0f);
        pixels += topPixels;

        float height = pixels * LocalPerPixel;
        if (body != null)
        {
            body.height = Mathf.Max(height, body.radius * 2f);
            body.center = new Vector3(body.center.x, height * 0.5f, body.center.z);
        }
        if (thorns != null)
        {
            thorns.size = new Vector3(thorns.size.x, height, thorns.size.z);
            thorns.center = new Vector3(thorns.center.x, height * 0.5f, thorns.center.z);
        }
        if (shadowCaster != null) shadowCaster.SetHeight(height * transform.lossyScale.y);
    }
}
