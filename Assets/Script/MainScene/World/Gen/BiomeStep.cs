using System;
using UnityEngine;

// 청크의 바이옴을 정함
// - 대형 구역(regions): 맵의 정해진 자리에 큰 바이옴을 놓음 (중앙 숲, 북쪽 설원 등). 시드마다 배치 전체를 회전
// - 남는 자리: 보로노이 셀마다 소형 바이옴을 무작위로 뽑음
// - 기준점(anchors): 배치와 함께 회전하는 지점 (구조물이 위치를 정할 때 사용, 예: 설원 반대편의 보스 둥지)
// 위치는 맵 한 변의 절반을 1로 한 비율이라 맵 크기와 관계없이 같은 배치가 됨 (+y = 회전 전 북쪽)
// layoutSinceVersion보다 먼저 만든 월드는 예전 방식(펄린 노이즈 임계값)을 그대로 씀 (경계 밖 청크가 바뀌지 않도록)
[CreateAssetMenu(fileName = "BiomeStep", menuName = "World/Gen/BiomeStep")]
public class BiomeStep : WorldGenStep
{
    [Serializable]
    public class Region
    {
        public BiomeData biome;
        [Tooltip("후보 위치 (맵 절반 = 1). 여러 개면 시드마다 하나를 고름")]
        public Vector2[] positions = { Vector2.zero };
        [Tooltip("다른 구역(목록 번호)이 고른 위치를 그대로 씀 (-1이면 사용 안 함). 예: 사막 가운데 화산")]
        public int sameSpotAs = -1;
        [Tooltip("반경 (맵 절반 = 1)")]
        [Min(0f)] public float radius = 0.3f;
        [Tooltip("작은 맵에서도 이 청크 수보다 작아지지 않음")]
        [Min(0f)] public float minRadiusChunks;
    }

    [Serializable]
    public class Anchor
    {
        public string id;
        [Tooltip("위치 (맵 절반 = 1, 배치와 함께 회전)")]
        public Vector2 position;
    }

    [Serializable]
    public struct SmallBiome
    {
        public BiomeData biome;
        [Min(0f)] public float weight;
    }

    [Tooltip("이 버전부터 만든 월드에 구역 배치를 씀 (이전 월드는 예전 노이즈 방식)")]
    [SerializeField] private int layoutSinceVersion = 3;

    [Header("대형 구역 (위에 있는 것이 우선)")]
    [SerializeField] private Region[] regions;
    [Tooltip("배치 전체를 시드마다 무작위 각도로 회전")]
    [SerializeField] private bool randomRotation = true;

    [Header("가장자리")]
    [Tooltip("경계를 흔드는 정도 (맵 절반 = 1)")]
    [SerializeField, Min(0f)] private float edgeWarp = 0.12f;
    [Tooltip("흔들림 노이즈 크기 (청크 단위 주기의 역수)")]
    [SerializeField, Min(0.01f)] private float edgeNoiseScale = 0.15f;

    [Header("소형 바이옴 (구역 밖)")]
    [SerializeField] private SmallBiome[] smallBiomes;
    [Tooltip("소형 바이옴 셀 크기 (청크)")]
    [SerializeField, Min(1f)] private float cellChunks = 3f;

    [Header("구조물 기준점")]
    [SerializeField] private Anchor[] anchors;

    [Header("예전 방식 (layoutSinceVersion 이전 월드)")]
    [SerializeField] private float noiseScale = 0.1f;

    // 시드마다 한 번만 정하는 배치 (이웃 청크들이 반복해서 물어봄)
    private class Layout
    {
        public float angle;
        public Vector2[] centers;
        public float warpOffsetX, warpOffsetZ;
        public int cellSalt;
    }

    private WorldGenContext cachedContext;
    private Layout cachedLayout;

    public override void GenerateChunk(WorldGenContext ctx, Vector2Int coord, ChunkData chunk)
    {
        chunk.biome = ctx.BiomeIndex(Evaluate(ctx, coord));
    }

    // 청크 데이터 없이 좌표만으로 바이옴을 계산 (아직 생성되지 않은 이웃 청크를 보는 단계가 사용)
    public BiomeData Evaluate(WorldGenContext ctx, Vector2Int coord)
    {
        if (ctx.genVersion < layoutSinceVersion) return EvaluateLegacy(ctx, coord);

        Layout layout = GetLayout(ctx);
        float half = HalfSize(ctx);
        // 청크 중심 (청크 단위, 맵 중심 = 0)
        var point = new Vector2(coord.x + 0.5f, coord.y + 0.5f);

        // 경계가 원이 아니라 덩어리 모양이 되도록 좌표를 노이즈로 밀어 봄
        float warpX = Mathf.PerlinNoise(point.x * edgeNoiseScale + layout.warpOffsetX, point.y * edgeNoiseScale + layout.warpOffsetZ) - 0.5f;
        float warpZ = Mathf.PerlinNoise(point.x * edgeNoiseScale + layout.warpOffsetZ, point.y * edgeNoiseScale - layout.warpOffsetX) - 0.5f;
        Vector2 local = Rotate(point / half, -layout.angle) + new Vector2(warpX, warpZ) * (edgeWarp * 2f);

        for (int i = 0; i < regions.Length; i++)
        {
            Region region = regions[i];
            if (region.biome == null) continue;

            float radius = Mathf.Max(region.radius, region.minRadiusChunks / half);
            if ((local - layout.centers[i]).sqrMagnitude < radius * radius) return region.biome;
        }

        return SmallBiomeAt(ctx, layout, point + new Vector2(warpX, warpZ) * cellChunks);
    }

    // 기준점의 월드 위치 (회전 적용). 없는 id면 false
    public bool TryGetAnchor(WorldGenContext ctx, string id, out Vector3 position)
    {
        position = default;
        if (anchors == null || ctx.genVersion < layoutSinceVersion) return false;

        foreach (var anchor in anchors)
        {
            if (anchor.id != id) continue;

            Vector2 chunks = Rotate(anchor.position, GetLayout(ctx).angle) * HalfSize(ctx);
            position = new Vector3(chunks.x * ctx.ChunkSize, 0f, chunks.y * ctx.ChunkSize);
            return true;
        }
        return false;
    }

    private static float HalfSize(WorldGenContext ctx) => Mathf.Max(1f, Mathf.Min(ctx.map.width, ctx.map.height) * 0.5f);

    private Layout GetLayout(WorldGenContext ctx)
    {
        if (cachedContext == ctx && cachedLayout != null) return cachedLayout;

        var prng = ctx.StepRandom(this);
        var layout = new Layout
        {
            angle = randomRotation ? (float)prng.NextDouble() * 360f : 0f,
            centers = new Vector2[regions.Length],
            warpOffsetX = prng.Next(-100000, 100000),
            warpOffsetZ = prng.Next(-100000, 100000),
            cellSalt = prng.Next(),
        };

        // 후보 고르기 → 같은 자리 따라가기 순서 (따라갈 구역이 목록 뒤에 있어도 됨)
        for (int i = 0; i < regions.Length; i++)
        {
            Vector2[] options = regions[i].positions;
            layout.centers[i] = options != null && options.Length > 0 ? options[prng.Next(options.Length)] : Vector2.zero;
        }
        for (int i = 0; i < regions.Length; i++)
        {
            int target = regions[i].sameSpotAs;
            if (target >= 0 && target < regions.Length && target != i) layout.centers[i] = layout.centers[target];
        }

        cachedContext = ctx;
        cachedLayout = layout;
        return layout;
    }

    // 지터 격자 보로노이: 가장 가까운 셀 중심의 바이옴
    private BiomeData SmallBiomeAt(WorldGenContext ctx, Layout layout, Vector2 point)
    {
        Vector2 cellPoint = point / cellChunks;
        int baseX = Mathf.FloorToInt(cellPoint.x);
        int baseZ = Mathf.FloorToInt(cellPoint.y);

        float best = float.MaxValue;
        Vector2Int bestCell = default;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                var cell = new Vector2Int(baseX + dx, baseZ + dz);
                var prng = CellRandom(layout, cell);
                var feature = new Vector2(cell.x + (float)prng.NextDouble(), cell.y + (float)prng.NextDouble());
                float distance = (feature - cellPoint).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    bestCell = cell;
                }
            }
        }

        // 위치를 뽑은 난수를 이어서 써서 셀마다 바이옴을 고정
        var pick = CellRandom(layout, bestCell);
        pick.NextDouble();
        pick.NextDouble();
        return PickSmallBiome(ctx, (float)pick.NextDouble());
    }

    private static System.Random CellRandom(Layout layout, Vector2Int cell)
    {
        unchecked
        {
            return new System.Random(layout.cellSalt ^ (cell.x * 73856093) ^ (cell.y * 19349663));
        }
    }

    private BiomeData PickSmallBiome(WorldGenContext ctx, float roll01)
    {
        float total = 0f;
        if (smallBiomes != null)
        {
            foreach (var entry in smallBiomes)
            {
                if (entry.biome != null) total += entry.weight;
            }
        }
        if (total <= 0f) return ctx.Biomes.biomes.Length > 0 ? ctx.Biomes.biomes[0] : null;

        float roll = roll01 * total;
        foreach (var entry in smallBiomes)
        {
            if (entry.biome == null || entry.weight <= 0f) continue;
            roll -= entry.weight;
            if (roll < 0f) return entry.biome;
        }
        return smallBiomes[smallBiomes.Length - 1].biome;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    #region 예전 방식

    private BiomeData EvaluateLegacy(WorldGenContext ctx, Vector2Int coord)
    {
        // 시드마다 노이즈의 다른 구역을 쓰도록 오프셋을 줌
        var prng = ctx.StepRandom(this);
        float offsetX = prng.Next(-100000, 100000);
        float offsetZ = prng.Next(-100000, 100000);

        float noiseValue = Mathf.PerlinNoise(
            coord.x * noiseScale + offsetX,
            coord.y * noiseScale + offsetZ);

        return Resolve(ctx.Biomes, noiseValue);
    }

    private static BiomeData Resolve(BiomeDatabase database, float noiseValue)
    {
        var biomes = database.biomes;
        foreach (var biome in biomes)
        {
            if (noiseValue >= biome.minThreshold && noiseValue < biome.maxThreshold)
                return biome;
        }

        return biomes.Length > 0 ? biomes[0] : null;
    }

    #endregion
}
