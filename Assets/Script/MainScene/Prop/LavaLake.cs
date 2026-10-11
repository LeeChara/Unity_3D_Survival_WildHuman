using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 용암 호수: 위치를 시드로 모양·크기를 만들어 호수마다 생김새가 다름
// 같은 위치면 항상 같은 모양이므로 세이브에는 위치만 있으면 됨
// 칸 중심마다 용암인지 정하고, 용암 칸은 꽉 찬 사각형 타일로 깔아 그림 (마인크래프트 블록처럼)
// 피해 판정도 같은 칸 지도를 보므로 그림과 정확히 일치함
// 원 배치(Layout)는 월드 생성(LavaStep)도 같이 써서, 생성 시점에 호수가 차지할 반경을 미리 앎
public class LavaLake : MonoBehaviour, IHazardArea
{
    private const float Threshold = 0.12f;
    private const int CacheLimit = 32;

    [Header("참조")]
    [Tooltip("바닥에 눕힌 타일맵 (부모 Grid의 Cell Swizzle은 XZY, Tilemap Orientation은 XZ)")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private LavaTileSet tileSet;
    [Tooltip("호수를 감싸는 트리거 (반경은 모양에 맞춰 바뀜)")]
    [SerializeField] private SphereCollider trigger;

    [Header("모양")]
    [Tooltip("호수 지름 범위 (m)")]
    [SerializeField] private Vector2 diameterRange = new Vector2(20f, 30f);
    [Tooltip("겹쳐서 호수를 이루는 원의 개수 범위 (클수록 갈래가 많음)")]
    [SerializeField] private Vector2Int blobCount = new Vector2Int(3, 6);

    [Header("빛")]
    [Tooltip("호수 안에 나눠 두는 광원 최대 개수")]
    [SerializeField, Range(1, 8)] private int maxLights = 6;
    [Tooltip("광원 사이 최소 간격 (m)")]
    [SerializeField] private float lightSpacing = 20f;
    [Tooltip("광원 하나의 반경 (광원이 하나뿐이면 호수 반경 + lightPadding)")]
    [SerializeField] private float lightRadius = 14f;
    [SerializeField] private float lightPadding = 3f;
    [SerializeField] private float lightIntensity = 1.2f;
    [SerializeField] private Color lightColor = new Color(1f, 0.5f, 0.2f);
    [SerializeField, Range(0f, 0.5f)] private float lightFlicker = 0.2f;

    // 위치에서 만든 모양 (같은 위치의 호수끼리 공유)
    private class Shape
    {
        public int cells;          // 한 변의 칸 수
        public bool[] lava;        // 칸마다 용암인지
        public TileBase[] tiles;   // 칸마다 놓을 타일 (땅이면 null)
        public float radius;       // 원 배치 기준 최대 반경 (m)
        public List<Vector3> lightSpots;
    }

    private static readonly Dictionary<long, Shape> cache = new();
    private static readonly List<Vector4> blobBuffer = new();
    // 타일 세트마다 용암 타일 하나 (실행 중에 한 번만 만듦)
    private static readonly Dictionary<LavaTileSet, LavaTile> tileBySet = new();

    private LightSource[] lights;
    private Shape shape;
    private Vector3 builtAt;

    private float CellSize => tileSet.CellSize;

    // 원 배치가 넘지 않는 최대 반경 (월드 생성 시 청크 안쪽으로 띄울 거리)
    public float MaxRadius => diameterRange.y * 0.5f;

    private void Awake()
    {
        lights = new LightSource[maxLights];
        for (int i = 0; i < maxLights; i++)
        {
            var child = new GameObject("Glow" + i);
            child.transform.SetParent(transform, false);
            lights[i] = child.AddComponent<LightSource>();
            child.SetActive(false);
        }
    }

    // 풀에서 꺼낸 직후에는 위치가 아직 정해지지 않았으므로, 다음 Update에서 새 위치로 만들 때까지 비워 둠
    private void OnEnable()
    {
        shape = null;
        tilemap.ClearAllTiles();
    }

    private void Update()
    {
        Vector3 position = transform.position;
        if (shape == null || position != builtAt) Build(position);
    }

    // 발밑 칸이 용암 칸인지 (그림과 같은 칸 지도)
    public bool Contains(Vector3 worldPosition)
    {
        if (shape == null) return false;

        float half = shape.cells * CellSize * 0.5f;
        Vector3 offset = worldPosition - transform.position;
        int x = Mathf.FloorToInt((offset.x + half) / CellSize);
        int z = Mathf.FloorToInt((offset.z + half) / CellSize);
        if (x < 0 || z < 0 || x >= shape.cells || z >= shape.cells) return false;
        return shape.lava[z * shape.cells + x];
    }

    #region 원 배치 (월드 생성과 공유)

    // 위치마다 정해지는 원 배치 (x, z = 호수 중심 기준 m, w = 반지름 m), 반환값은 모든 원을 덮는 반경
    // 노이즈는 원 안에서만 더하므로 실제 모양은 이 반경을 넘지 않음
    public float Layout(Vector3 position, List<Vector4> blobs, out Vector4 noiseOffsets)
    {
        var rng = new System.Random(KeyHash(Key(position)));
        float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        float maxRadius = Range(diameterRange.x, diameterRange.y) * 0.5f;
        int count = rng.Next(blobCount.x, blobCount.y + 1);

        blobs?.Clear();
        float mainRadius = maxRadius * Range(0.55f, 0.68f);
        blobs?.Add(new Vector4(0f, 0f, 0f, mainRadius));
        float bound = mainRadius;

        // 작은 원은 방향이 겹치지 않게 고르게 돌려 가며 바깥쪽에 붙여 갈래가 드러나게 함
        float startAngle = Range(0f, Mathf.PI * 2f);
        for (int i = 1; i < count; i++)
        {
            float r = maxRadius * Range(0.28f, 0.45f);
            float angle = startAngle + (i - 1) * Mathf.PI * 2f / (count - 1) + Range(-0.4f, 0.4f);
            float distance = Mathf.Min(maxRadius * Range(0.45f, 0.75f), maxRadius - r);
            blobs?.Add(new Vector4(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance, r));
            bound = Mathf.Max(bound, distance + r);
        }

        noiseOffsets = new Vector4(Range(0f, 1000f), Range(0f, 1000f), Range(0f, 1000f), Range(0f, 1000f));
        return bound;
    }

    public float LayoutRadius(Vector3 position) => Layout(position, null, out _);

    #endregion

    // 세이브 위치는 소수 둘째 자리까지이므로 같은 호수는 항상 같은 키
    private static long Key(Vector3 position)
    {
        return ((long)Mathf.RoundToInt(position.x * 100f) << 32) ^ (uint)Mathf.RoundToInt(position.z * 100f);
    }

    private static int KeyHash(long key) => unchecked((int)(key ^ (key >> 32)));

    private void Build(Vector3 position)
    {
        builtAt = position;

        long key = Key(position);
        if (!cache.TryGetValue(key, out shape))
        {
            if (cache.Count >= CacheLimit) cache.Clear();
            shape = Generate(position);
            cache[key] = shape;
        }

        // 타일맵의 (0, 0) 칸이 모양 지도의 왼쪽 아래 칸이 되도록 Grid를 옮김
        float half = shape.cells * CellSize * 0.5f;
        tilemap.layoutGrid.transform.localPosition = new Vector3(-half, tilemap.layoutGrid.transform.localPosition.y, -half);
        tilemap.ClearAllTiles();
        tilemap.SetTilesBlock(new BoundsInt(0, 0, 0, shape.cells, shape.cells, 1), shape.tiles);

        // 트리거는 땅속에 중심을 둔 구라서 지면 높이에서 호수 반경을 덮도록 키움
        float depthBelow = -trigger.center.y;
        trigger.radius = Mathf.Sqrt(shape.radius * shape.radius + depthBelow * depthBelow);

        PlaceLights();
    }

    private void PlaceLights()
    {
        for (int i = 0; i < lights.Length; i++)
        {
            bool used = i < shape.lightSpots.Count;
            lights[i].gameObject.SetActive(used);
            if (!used) continue;

            lights[i].transform.localPosition = shape.lightSpots[i];
            float radius = shape.lightSpots.Count == 1 ? shape.radius + lightPadding : lightRadius;
            lights[i].Configure(radius, lightIntensity, lightColor, lightFlicker);
        }
    }

    private Shape Generate(Vector3 position)
    {
        float radius = Layout(position, blobBuffer, out Vector4 noise);
        float cell = CellSize;

        // 가장자리 밖으로 한 칸 이상 여유를 둬서 바깥 칸이 항상 땅이 되게 함
        int cells = Mathf.CeilToInt((radius + cell * 2f) * 2f / cell);
        float half = cells * cell * 0.5f;

        // 해안선 노이즈는 호수 크기에 비례한 큰 굴곡 + 크기와 상관없는 잔굴곡
        float coarse = 3f / radius, detail = 9f / radius, fine = 0.25f;

        // 칸 중심마다 판정
        var inside = new bool[cells * cells];
        for (int y = 0; y < cells; y++)
        {
            float z = (y + 0.5f) * cell - half;
            for (int x = 0; x < cells; x++)
            {
                float px = (x + 0.5f) * cell - half;

                // 메타볼: 원마다 가운데 1 → 가장자리 0으로 줄어드는 값을 더함
                float value = 0f;
                foreach (Vector4 blob in blobBuffer)
                {
                    float dx = px - blob.x, dz = z - blob.z;
                    float t = 1f - (dx * dx + dz * dz) / (blob.w * blob.w);
                    if (t > 0f) value += t * t;
                }

                // 어느 원에도 닿지 않는 곳은 노이즈로 번지지 않도록 바로 밖으로 처리
                if (value > 0f)
                {
                    value += (Mathf.PerlinNoise(px * coarse + noise.x, z * coarse + noise.y) - 0.5f) * 0.5f
                           + (Mathf.PerlinNoise(px * detail + noise.y, z * detail + noise.x) - 0.5f) * 0.15f
                           + (Mathf.PerlinNoise(px * fine + noise.z, z * fine + noise.w) - 0.5f) * 0.06f;
                }

                inside[y * cells + x] = value > Threshold;
            }
        }

        // 노이즈로 떨어져 나간 작은 조각은 버리고 가운데와 이어진 칸만 남김
        bool[] lava = KeepConnected(inside, cells, cells / 2 * cells + cells / 2);

        LavaTile tile = TileFor(tileSet);
        var tiles = new TileBase[cells * cells];
        for (int i = 0; i < tiles.Length; i++) tiles[i] = lava[i] ? tile : null;

        return new Shape
        {
            cells = cells,
            lava = lava,
            tiles = tiles,
            radius = radius,
            lightSpots = PickLightSpots(lava, cells, half),
        };
    }

    private static LavaTile TileFor(LavaTileSet set)
    {
        if (tileBySet.TryGetValue(set, out LavaTile tile)) return tile;

        tile = ScriptableObject.CreateInstance<LavaTile>();
        tile.set = set;
        tileBySet[set] = tile;
        return tile;
    }

    // 호수 안에서 서로 lightSpacing 이상 떨어진 지점을, 가운데부터 가장 먼 곳 순으로 고름
    private List<Vector3> PickLightSpots(bool[] lava, int cells, float half)
    {
        float cell = CellSize;
        var candidates = new List<Vector3>();
        int stride = Mathf.Max(1, Mathf.RoundToInt(lightSpacing * 0.25f / cell));
        for (int y = 0; y < cells; y += stride)
        {
            for (int x = 0; x < cells; x += stride)
            {
                if (lava[y * cells + x]) candidates.Add(new Vector3((x + 0.5f) * cell - half, 0.5f, (y + 0.5f) * cell - half));
            }
        }

        var spots = new List<Vector3> { new Vector3(0f, 0.5f, 0f) };
        while (spots.Count < maxLights)
        {
            Vector3 best = default;
            float bestDistance = lightSpacing;
            foreach (Vector3 candidate in candidates)
            {
                float nearest = float.MaxValue;
                foreach (Vector3 spot in spots) nearest = Mathf.Min(nearest, (candidate - spot).sqrMagnitude);
                nearest = Mathf.Sqrt(nearest);
                if (nearest > bestDistance)
                {
                    bestDistance = nearest;
                    best = candidate;
                }
            }
            if (bestDistance <= lightSpacing) break;
            spots.Add(best);
        }
        return spots;
    }

    private static bool[] KeepConnected(bool[] inside, int size, int start)
    {
        var connected = new bool[inside.Length];
        if (!inside[start]) return connected;

        var queue = new Queue<int>();
        connected[start] = true;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % size, y = i / size;
            Visit(x + 1, y); Visit(x - 1, y); Visit(x, y + 1); Visit(x, y - 1);
        }
        return connected;

        void Visit(int x, int y)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return;
            int i = y * size + x;
            if (!inside[i] || connected[i]) return;
            connected[i] = true;
            queue.Enqueue(i);
        }
    }
}
