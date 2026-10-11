using System.Collections.Generic;
using UnityEngine;

// 용암 호수(소형·중형·거대)를 배치하는 단계 (자원 배치 단계보다 앞에 둘 것)
// 어느 청크든 생성 순서와 상관없이 같은 결과를 내도록, 배치는 시드와 좌표만으로 계산
// - 소형·중형: 자기 청크 안에 다 들어가게 놓음 (청크 경계를 넘지 않음)
// - 거대: 청크 3×3 묶음 안에 다 들어가게 묶음마다 최대 하나, 덮는 청크가 모두 대상 바이옴일 때만
// 그래서 한 청크에 걸치는 호수는 "자기 청크의 호수 + 자기 묶음의 거대 호수"뿐이고, PropStep은 이것만 피하면 됨
[CreateAssetMenu(fileName = "LavaStep", menuName = "World/Gen/LavaStep")]
public class LavaStep : WorldGenStep, IFootprintProvider
{
    private const int GroupChunks = 3;

    [Tooltip("이웃 청크의 바이옴을 계산할 때 쓰는 바이옴 단계")]
    [SerializeField] private BiomeStep biomeStep;
    [Tooltip("용암 호수가 생기는 바이옴")]
    [SerializeField] private BiomeData biome;
    [Tooltip("호수 가장자리끼리 띄울 최소 거리 (m)")]
    [SerializeField, Min(0f)] private float gap = 6f;

    [Header("거대")]
    [SerializeField] private LavaLake giantPrefab;
    [Tooltip("청크 3×3 묶음마다 거대 호수를 시도할 확률 (덮는 청크가 모두 대상 바이옴이어야 실제로 생김)")]
    [SerializeField, Range(0f, 1f)] private float giantChance = 1f;

    [Header("중형")]
    [SerializeField] private LavaLake mediumPrefab;
    [Tooltip("청크마다 중형 호수가 생길 확률")]
    [SerializeField, Range(0f, 1f)] private float mediumChance = 0.2f;

    [Header("소형")]
    [SerializeField] private LavaLake smallPrefab;
    [Tooltip("청크마다 소형 호수 개수 범위")]
    [SerializeField] private Vector2Int smallCount = new Vector2Int(0, 2);
    [SerializeField, Min(1)] private int placementAttempts = 10;

    private struct Planned
    {
        public LavaLake prefab;
        public Vector3 position;
        public float radius;
    }

    // 같은 생성 과정에서 묶음마다 거대 호수를 한 번만 계산 (이웃 청크들이 반복해서 물어봄)
    private WorldGenContext cachedContext;
    private readonly Dictionary<Vector2Int, Planned?> giants = new();

    public override IEnumerable<GameObject> ReferencedProps
    {
        get
        {
            if (giantPrefab != null) yield return giantPrefab.gameObject;
            if (mediumPrefab != null) yield return mediumPrefab.gameObject;
            if (smallPrefab != null) yield return smallPrefab.gameObject;
        }
    }

    public override void GenerateChunk(WorldGenContext ctx, Vector2Int coord, ChunkData chunk)
    {
        foreach (Planned lake in PlanChunk(ctx, coord))
        {
            chunk.AddProp(ctx.PropIndex(lake.prefab.gameObject), lake.position.x, lake.position.z);
        }
    }

    // 다른 청크에 중심이 있지만 이 청크까지 덮는 거대 호수 (자기 청크의 호수는 이미 청크 자원에 있음)
    public void AddFootprints(WorldGenContext ctx, Vector2Int coord, List<Vector3> positions, List<float> radii)
    {
        Planned? giant = PlanGiant(ctx, GroupOf(coord));
        if (giant == null || ctx.WorldToChunk(giant.Value.position) == coord) return;

        positions.Add(giant.Value.position);
        radii.Add(PropFootprint.RadiusOf(giant.Value.prefab.gameObject, giant.Value.position));
    }

    private List<Planned> PlanChunk(WorldGenContext ctx, Vector2Int coord)
    {
        var lakes = new List<Planned>();
        if (biomeStep.Evaluate(ctx, coord) != biome) return lakes;

        Planned? giant = PlanGiant(ctx, GroupOf(coord));
        if (giant != null && ctx.WorldToChunk(giant.Value.position) == coord) lakes.Add(giant.Value);

        var prng = ctx.ChunkRandom(this, coord);
        if (mediumPrefab != null && prng.NextDouble() < mediumChance)
        {
            TryPlace(ctx, prng, coord, mediumPrefab, giant, lakes);
        }

        int count = smallPrefab != null ? prng.Next(smallCount.x, smallCount.y + 1) : 0;
        for (int i = 0; i < count; i++)
        {
            TryPlace(ctx, prng, coord, smallPrefab, giant, lakes);
        }
        return lakes;
    }

    // 청크 안쪽에 다 들어가는 위치 중 다른 호수와 겹치지 않는 곳
    private void TryPlace(WorldGenContext ctx, System.Random prng, Vector2Int coord, LavaLake prefab, Planned? giant, List<Planned> lakes)
    {
        Vector3 origin = ctx.ChunkOrigin(coord);
        float inset = prefab.MaxRadius;
        float span = ctx.ChunkSize - inset * 2f;
        if (span <= 0f) return;

        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            Vector3 position = Round(origin + new Vector3(
                inset + (float)prng.NextDouble() * span, 0f,
                inset + (float)prng.NextDouble() * span));
            float radius = prefab.LayoutRadius(position);

            if (giant != null && Overlaps(position, radius, giant.Value)) continue;

            bool blocked = false;
            foreach (Planned other in lakes)
            {
                if (Overlaps(position, radius, other))
                {
                    blocked = true;
                    break;
                }
            }
            if (blocked) continue;

            lakes.Add(new Planned { prefab = prefab, position = position, radius = radius });
            return;
        }
    }

    // 묶음 안쪽에 다 들어가게 놓고, 덮는 청크가 모두 대상 바이옴인지 확인
    private Planned? PlanGiant(WorldGenContext ctx, Vector2Int group)
    {
        if (cachedContext != ctx)
        {
            cachedContext = ctx;
            giants.Clear();
        }
        if (giants.TryGetValue(group, out Planned? cached)) return cached;

        Planned? result = null;
        if (giantPrefab != null)
        {
            var prng = GroupRandom(ctx, group);
            if (prng.NextDouble() < giantChance)
            {
                float groupSize = ctx.ChunkSize * GroupChunks;
                float inset = giantPrefab.MaxRadius;
                float span = groupSize - inset * 2f;
                Vector3 origin = ctx.ChunkOrigin(group * GroupChunks);

                if (span > 0f)
                {
                    Vector3 position = Round(origin + new Vector3(
                        inset + (float)prng.NextDouble() * span, 0f,
                        inset + (float)prng.NextDouble() * span));
                    float radius = giantPrefab.LayoutRadius(position);

                    if (CoversOnlyBiome(ctx, position, radius))
                    {
                        result = new Planned { prefab = giantPrefab, position = position, radius = radius };
                    }
                }
            }
        }

        giants[group] = result;
        return result;
    }

    private bool CoversOnlyBiome(WorldGenContext ctx, Vector3 center, float radius)
    {
        Vector2Int min = ctx.WorldToChunk(center - new Vector3(radius, 0f, radius));
        Vector2Int max = ctx.WorldToChunk(center + new Vector3(radius, 0f, radius));
        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                var coord = new Vector2Int(x, z);
                if (!CircleTouchesChunk(ctx, center, radius, coord)) continue;
                if (biomeStep.Evaluate(ctx, coord) != biome) return false;
            }
        }
        return true;
    }

    private static bool CircleTouchesChunk(WorldGenContext ctx, Vector3 center, float radius, Vector2Int coord)
    {
        Vector3 origin = ctx.ChunkOrigin(coord);
        float nearestX = Mathf.Clamp(center.x, origin.x, origin.x + ctx.ChunkSize);
        float nearestZ = Mathf.Clamp(center.z, origin.z, origin.z + ctx.ChunkSize);
        float dx = center.x - nearestX, dz = center.z - nearestZ;
        return dx * dx + dz * dz < radius * radius;
    }

    private bool Overlaps(Vector3 position, float radius, Planned other)
    {
        float limit = radius + other.radius + gap;
        return (position - other.position).sqrMagnitude < limit * limit;
    }

    private static Vector2Int GroupOf(Vector2Int coord)
    {
        return new Vector2Int(Mathf.FloorToInt(coord.x / (float)GroupChunks), Mathf.FloorToInt(coord.y / (float)GroupChunks));
    }

    // 청크 난수와 겹치지 않도록 묶음 좌표에 다른 소수를 곱함
    private System.Random GroupRandom(WorldGenContext ctx, Vector2Int group)
    {
        unchecked
        {
            return new System.Random(ctx.StepSeed(this) ^ (group.x * 83492791) ^ (group.y * 50331653) ^ 0x5bd1e995);
        }
    }

    // 세이브 위치와 같게 소수 둘째 자리까지 (호수 모양 키가 같아지도록)
    private static Vector3 Round(Vector3 position)
    {
        return new Vector3(Mathf.Round(position.x * 100f) / 100f, 0f, Mathf.Round(position.z * 100f) / 100f);
    }
}
