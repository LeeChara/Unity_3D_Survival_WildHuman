using System.Collections.Generic;
using UnityEngine;

// 월드 구조물을 배치하는 단계 (시작 지점 단계 뒤, 맨 마지막에 둘 것)
// 경계 안 맵 전체가 채워진 뒤(GenerateMap) 한 번만 정해서 저장 (경계 밖에는 생기지 않음)
// 1. 후보점: 맵 전체에 서로 candidateSpacing 이상 떨어진 점을 고르게 채움 (Poisson disk) → 몰림·빈 지역이 없음
//    기준점(BiomeStep anchor)을 쓰는 종류가 있으면 그 기준점을 먼저 후보로 넣고 나머지를 채움
// 2. 배정: 우선순위가 높은 종류부터 조건(시작 지점과의 거리 띠, 바이옴, 같은 종류 간격, 용암)에 맞는 후보를 차지
//    기준점이 있는 종류는 기준점 반경 안에서 가장 가까운 후보부터 차지
//    발견 보장(guaranteeNear)이면 시작 지점 근처 띠에서 하나를 먼저 놓고, 자리가 모자라면 조건을 단계적으로 완화
// 3. 놓인 구조물 반경 안의 자원은 지움 (용암 호수는 피하므로 지워지지 않음)
[CreateAssetMenu(fileName = "StructureStep", menuName = "World/Gen/StructureStep")]
public class StructureStep : WorldGenStep
{
    // count가 기준으로 삼는 맵 넓이 (작은 맵 16×16 청크)
    private const int BaseMapChunks = 16 * 16;
    // 후보점 하나 주변에서 다음 점을 찾는 시도 횟수 (Bridson 알고리즘의 k)
    private const int PoissonAttempts = 30;

    [Tooltip("후보점끼리 최소 거리 (m). 서로 다른 종류의 구조물도 최소 이만큼 떨어짐 (가장 큰 반경의 2배보다 커야 함)")]
    [SerializeField, Min(10f)] private float candidateSpacing = 200f;
    [Tooltip("맵 경계와 구조물 가장자리 사이 최소 거리 (m)")]
    [SerializeField, Min(0f)] private float borderMargin = 20f;
    [Tooltip("시작 지점과 구조물 가장자리 사이 최소 거리 (m, 모든 종류 공통)")]
    [SerializeField, Min(0f)] private float spawnClearance = 60f;
    [Tooltip("구조물 기준점(anchor)을 계산하는 바이옴 단계")]
    [SerializeField] private BiomeStep biomeStep;

    private class Placement
    {
        public WorldStructureData data;
        public Vector3 position;
    }

    // 조건 완화 단계: 0 = 그대로, 1 = 같은 종류 간격 절반, 2 = 거리 띠 무시
    private const int RelaxLevels = 3;

    // 후보점마다 용암 호수(여유 범위 포함)까지 남은 거리 (배정 중 반복 계산하지 않도록 한 번만 구함)
    private float[] lakeClearance;

    public override void GenerateMap(WorldGenContext ctx)
    {
        WorldStructureDatabase database = ctx.config.structureDatabase;
        if (database == null) return;

        var types = new List<WorldStructureData>();
        foreach (var data in database.structures)
        {
            if (data != null && data.prefab != null && data.enabled && !string.IsNullOrEmpty(data.id)) types.Add(data);
        }
        if (types.Count == 0) return;
        // 우선순위가 같으면 목록 순서 유지 (List.Sort는 안정 정렬이 아니므로 순서 번호를 함께 비교)
        var order = new Dictionary<WorldStructureData, int>();
        for (int i = 0; i < types.Count; i++) order[types[i]] = i;
        types.Sort((a, b) => a.priority != b.priority ? b.priority.CompareTo(a.priority) : order[a].CompareTo(order[b]));

        var prng = ctx.StepRandom(this);
        Vector3 spawn = new(ctx.map.spawnX, 0f, ctx.map.spawnZ);
        float areaScale = Mathf.Max(1f, ctx.map.width * ctx.map.height / (float)BaseMapChunks);

        // 기준점 (예전 월드이거나 없는 id면 기준점 없이 배치)
        var anchors = new Dictionary<WorldStructureData, Vector3>();
        var seeds = new List<Vector3>();
        foreach (var data in types)
        {
            if (string.IsNullOrEmpty(data.anchor) || biomeStep == null) continue;
            if (!biomeStep.TryGetAnchor(ctx, data.anchor, out Vector3 anchor))
            {
                Debug.LogWarning($"[StructureStep] '{data.id}'의 기준점 '{data.anchor}'이 BiomeStep에 없음");
                continue;
            }
            anchors[data] = anchor;
            if (IsInsideMap(ctx, anchor, data.radius + borderMargin)) seeds.Add(anchor);
        }

        List<Vector3> candidates = PoissonCandidates(ctx, prng, seeds);
        var used = new bool[candidates.Count];
        float maxRadius = 0f;
        foreach (var data in types) maxRadius = Mathf.Max(maxRadius, data.radius);
        lakeClearance = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            lakeClearance[i] = LakeClearance(ctx, candidates[i], maxRadius);
        }
        var placements = new List<Placement>();

        foreach (var data in types)
        {
            int target = RollCount(prng, data, data.scaleCountWithMap ? areaScale : 1f);
            int placed = 0;
            Vector3? anchor = anchors.TryGetValue(data, out Vector3 found) ? found : null;

            if (data.guaranteeNear && target > 0)
            {
                // 보장 띠에서도 완화 단계를 거치며 찾음 (띠 자체는 지킴)
                for (int relax = 0; relax < RelaxLevels - 1 && placed == 0; relax++)
                {
                    if (TryAssign(ctx, prng, data, candidates, used, placements, spawn, data.nearBand, relax, anchor)) placed++;
                }
            }

            for (int relax = 0; relax < RelaxLevels && placed < target; relax++)
            {
                Vector2 band = relax >= 2 ? Vector2.zero : data.distanceBand;
                while (placed < target && TryAssign(ctx, prng, data, candidates, used, placements, spawn, band, relax, anchor)) placed++;
            }

            if (placed < target) Debug.Log($"[StructureStep] '{data.id}' {target}개 중 {placed}개만 배치 (조건에 맞는 자리 부족)");
        }

        foreach (var placement in placements)
        {
            ClearProps(ctx, placement.position, placement.data.radius);
            ctx.map.structures.Add(new StructureSite
            {
                type = ctx.StructureIndex(placement.data),
                x = placement.position.x,
                z = placement.position.z,
                rotY = placement.data.randomRotation ? prng.Next(4) * 90f : 0f,
            });
        }
    }

    // 범위를 먼저 넓이에 비례해서 늘린 뒤 그 안에서 고름 (곱하기 전에 고르면 중간 값이 나오지 않음)
    private static int RollCount(System.Random prng, WorldStructureData data, float areaScale)
    {
        int min = Mathf.RoundToInt(Mathf.Min(data.count.x, data.count.y) * areaScale);
        int max = Mathf.RoundToInt(Mathf.Max(data.count.x, data.count.y) * areaScale);
        return prng.Next(min, max + 1);
    }

    // 조건에 맞는 후보 중 하나를 차지 (기준점이 있으면 가장 가까운 것, 없으면 무작위)
    private bool TryAssign(WorldGenContext ctx, System.Random prng, WorldStructureData data, List<Vector3> candidates,
        bool[] used, List<Placement> placements, Vector3 spawn, Vector2 band, int relax, Vector3? anchor)
    {
        float sameSpacing = relax >= 1 ? data.sameTypeSpacing * 0.5f : data.sameTypeSpacing;

        var eligible = new List<int>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (used[i]) continue;
            Vector3 position = candidates[i];

            float distance = Vector3.Distance(position, spawn);
            if (distance < band.x || (band.y > 0f && distance > band.y)) continue;
            if (anchor != null && (position - anchor.Value).sqrMagnitude > data.anchorRadius * data.anchorRadius) continue;
            if (lakeClearance[i] < data.radius) continue;
            if (!IsFreeSpot(ctx, data, position, placements, sameSpacing)) continue;
            eligible.Add(i);
        }
        if (eligible.Count == 0) return false;

        int chosen = eligible[prng.Next(eligible.Count)];
        if (anchor != null)
        {
            foreach (int i in eligible)
            {
                if ((candidates[i] - anchor.Value).sqrMagnitude < (candidates[chosen] - anchor.Value).sqrMagnitude) chosen = i;
            }
        }
        used[chosen] = true;
        placements.Add(new Placement { data = data, position = candidates[chosen] });
        return true;
    }

    private bool IsFreeSpot(WorldGenContext ctx, WorldStructureData data, Vector3 position, List<Placement> placements, float sameSpacing)
    {
        Vector3 spawn = new(ctx.map.spawnX, 0f, ctx.map.spawnZ);
        float clearance = spawnClearance + data.radius;
        if ((position - spawn).sqrMagnitude < clearance * clearance) return false;
        if (!IsInsideMap(ctx, position, data.radius + borderMargin)) return false;

        if (data.biomes != null && data.biomes.Length > 0)
        {
            ctx.map.TryGetChunk(ctx.WorldToChunk(position), out ChunkData center);
            if (System.Array.IndexOf(data.biomes, ctx.GetBiome(center)) < 0) return false;
        }

        foreach (var other in placements)
        {
            if (other.data != data) continue;
            if ((other.position - position).sqrMagnitude < sameSpacing * sameSpacing) return false;
        }

        return true;
    }

    private static bool IsInsideMap(WorldGenContext ctx, Vector3 position, float inset)
    {
        Vector2Int min = ctx.map.MinCoord();
        float xMin = min.x * ctx.ChunkSize, zMin = min.y * ctx.ChunkSize;
        float xMax = xMin + ctx.map.width * ctx.ChunkSize, zMax = zMin + ctx.map.height * ctx.ChunkSize;
        return position.x >= xMin + inset && position.x <= xMax - inset &&
               position.z >= zMin + inset && position.z <= zMax - inset;
    }

    // 호수(자원 중 TileLake: 용암·물)의 여유 범위 가장자리까지 남은 거리 (구조물 반경이 이보다 크면 닿음)
    // maxRadius보다 먼 호수는 어느 구조물에도 닿지 않으므로 그 너머는 보지 않음
    private static float LakeClearance(WorldGenContext ctx, Vector3 position, float maxRadius)
    {
        float clearance = float.PositiveInfinity;
        Vector3 reach = new(maxRadius + PropFootprint.MaxRadius, 0f, maxRadius + PropFootprint.MaxRadius);
        Vector2Int min = ctx.WorldToChunk(position - reach);
        Vector2Int max = ctx.WorldToChunk(position + reach);

        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                if (!ctx.map.TryGetChunk(new Vector2Int(x, z), out ChunkData chunk)) continue;
                for (int i = 0; i < chunk.PropCount(); i++)
                {
                    GameObject prefab = ctx.GetPropPrefab(chunk.types[i]);
                    if (prefab == null || !prefab.TryGetComponent(out TileLake _)) continue;

                    Vector3 prop = chunk.PropPosition(i);
                    clearance = Mathf.Min(clearance, Vector3.Distance(prop, position) - PropFootprint.RadiusOf(prefab, prop));
                }
            }
        }
        return clearance;
    }

    // 맵 전체를 서로 candidateSpacing 이상 떨어진 점으로 고르게 채움 (Bridson의 Poisson disk 샘플링)
    // seeds(월드 좌표)를 먼저 넣고 시작 (서로 너무 가까운 것은 뒤의 것을 버림)
    private List<Vector3> PoissonCandidates(WorldGenContext ctx, System.Random prng, List<Vector3> seeds)
    {
        Vector2Int minCoord = ctx.map.MinCoord();
        float originX = minCoord.x * ctx.ChunkSize, originZ = minCoord.y * ctx.ChunkSize;
        float width = ctx.map.width * ctx.ChunkSize, height = ctx.map.height * ctx.ChunkSize;

        float r = candidateSpacing;
        float cell = r / Mathf.Sqrt(2f);
        int cols = Mathf.CeilToInt(width / cell), rows = Mathf.CeilToInt(height / cell);
        var grid = new int[cols * rows];
        for (int i = 0; i < grid.Length; i++) grid[i] = -1;

        var points = new List<Vector2>();
        var active = new List<int>();

        void Add(Vector2 point)
        {
            points.Add(point);
            active.Add(points.Count - 1);
            grid[(int)(point.x / cell) + (int)(point.y / cell) * cols] = points.Count - 1;
        }

        bool Fits(Vector2 point)
        {
            if (point.x < 0f || point.y < 0f || point.x >= width || point.y >= height) return false;
            int cx = (int)(point.x / cell), cy = (int)(point.y / cell);
            for (int y = Mathf.Max(0, cy - 2); y <= Mathf.Min(rows - 1, cy + 2); y++)
            {
                for (int x = Mathf.Max(0, cx - 2); x <= Mathf.Min(cols - 1, cx + 2); x++)
                {
                    int index = grid[x + y * cols];
                    if (index >= 0 && (points[index] - point).sqrMagnitude < r * r) return false;
                }
            }
            return true;
        }

        foreach (Vector3 seed in seeds)
        {
            var point = new Vector2(seed.x - originX, seed.z - originZ);
            if (Fits(point)) Add(point);
        }
        if (points.Count == 0) Add(new Vector2((float)prng.NextDouble() * width, (float)prng.NextDouble() * height));
        while (active.Count > 0)
        {
            int slot = prng.Next(active.Count);
            Vector2 center = points[active[slot]];
            bool found = false;

            for (int attempt = 0; attempt < PoissonAttempts; attempt++)
            {
                float angle = (float)prng.NextDouble() * Mathf.PI * 2f;
                float distance = r * (1f + (float)prng.NextDouble());
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if (!Fits(point)) continue;

                Add(point);
                found = true;
                break;
            }

            if (!found)
            {
                active[slot] = active[active.Count - 1];
                active.RemoveAt(active.Count - 1);
            }
        }

        var result = new List<Vector3>(points.Count);
        foreach (Vector2 point in points)
        {
            result.Add(new Vector3(Round(originX + point.x), 0f, Round(originZ + point.y)));
        }
        return result;
    }

    // 반경 안의 자원을 지움 (용암 호수는 배정 때 이미 피했으므로 여기 들어오지 않음)
    private static void ClearProps(WorldGenContext ctx, Vector3 position, float radius)
    {
        Vector3 reach = new(radius, 0f, radius);
        Vector2Int min = ctx.WorldToChunk(position - reach);
        Vector2Int max = ctx.WorldToChunk(position + reach);

        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                if (!ctx.map.TryGetChunk(new Vector2Int(x, z), out ChunkData chunk)) continue;

                // 지우면 뒤 번호가 당겨지므로 뒤에서부터
                for (int i = chunk.PropCount() - 1; i >= 0; i--)
                {
                    if ((chunk.PropPosition(i) - position).sqrMagnitude < radius * radius) chunk.RemovePropAt(i);
                }
            }
        }
    }

    // 세이브 위치와 같게 소수 둘째 자리까지
    private static float Round(float value) => Mathf.Round(value * 100f) / 100f;
}
