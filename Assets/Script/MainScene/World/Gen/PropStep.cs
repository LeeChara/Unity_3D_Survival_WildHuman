using System.Collections.Generic;
using UnityEngine;

// 청크 바이옴의 propSpawnTable에 따라 자원 위치를 정함
[CreateAssetMenu(fileName = "PropStep", menuName = "World/Gen/PropStep")]
public class PropStep : WorldGenStep
{
    [Tooltip("minSpacing을 만족하는 위치를 찾기 위한 최대 시도 횟수")]
    [SerializeField] private int maxPlacementAttempts = 10;

    public override void GenerateChunk(WorldGenContext ctx, Vector2Int coord, ChunkData chunk)
    {
        BiomeData biome = ctx.GetBiome(chunk);
        if (biome == null || biome.propSpawnTable == null) return;

        var prng = ctx.ChunkRandom(this, coord);
        Vector3 chunkOrigin = ctx.ChunkOrigin(coord);

        // 앞 단계가 이미 놓은 것과도 간격을 지킴 (바닥을 넓게 차지하는 자원은 그 반경도 함께 기록)
        var placedPositions = new List<Vector3>();
        var placedRadii = new List<float>();
        for (int i = 0; i < chunk.PropCount(); i++)
        {
            var placed = new Vector3(chunk.px[i], 0f, chunk.pz[i]);
            placedPositions.Add(placed);
            placedRadii.Add(PropFootprint.RadiusOf(ctx.GetPropPrefab(chunk.types[i]), placed));
        }
        // 다른 청크에 놓였지만 이 청크까지 덮는 것 (여러 청크에 걸친 용암 호수 등)
        foreach (var step in ctx.Steps)
        {
            if (step is IFootprintProvider provider) provider.AddFootprints(ctx, coord, placedPositions, placedRadii);
        }

        // 테이블 순서대로 난수를 소비해야 결정론이 유지됨
        foreach (var entry in biome.propSpawnTable)
        {
            if (entry.propPrefab == null) continue;

            int type = ctx.PropIndex(entry.propPrefab);
            float footprint = PropFootprint.RadiusOf(entry.propPrefab, Vector3.zero);
            int count = entry.RollCount(prng);
            if (count == 0) continue;

            // 군집이면 중심을 먼저 정함 (군집이 청크 밖으로 나가지 않게 반경만큼 안쪽에서)
            Vector3 clusterCenter = default;
            if (entry.clusterRadius > 0f)
            {
                float inset = Mathf.Min(entry.clusterRadius, ctx.ChunkSize * 0.5f);
                clusterCenter = chunkOrigin + new Vector3(
                    inset + (float)prng.NextDouble() * (ctx.ChunkSize - inset * 2f),
                    0f,
                    inset + (float)prng.NextDouble() * (ctx.ChunkSize - inset * 2f));
            }

            for (int i = 0; i < count; i++)
            {
                if (!TryFindPosition(prng, entry, footprint, clusterCenter, chunkOrigin, ctx.ChunkSize, placedPositions, placedRadii, out Vector3 position))
                    continue;

                // 세이브 크기를 줄이기 위해 소수 둘째 자리까지만 저장
                position.x = Mathf.Round(position.x * 100f) / 100f;
                position.z = Mathf.Round(position.z * 100f) / 100f;

                placedPositions.Add(position);
                placedRadii.Add(footprint);
                chunk.AddProp(type, position.x, position.z);
            }
        }
    }

    // 간격 = minSpacing, 이미 놓인 자원의 점유 반경, 새 자원의 점유 반경 중 가장 큰 값
    private bool TryFindPosition(System.Random prng, PropSpawnEntry entry, float footprint, Vector3 clusterCenter,
        Vector3 chunkOrigin, float chunkSize, List<Vector3> placedPositions, List<float> placedRadii, out Vector3 position)
    {
        float baseSpacing = Mathf.Max(entry.minSpacing, footprint);

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            if (entry.clusterRadius > 0f)
            {
                // 원 안에 고르게 분포하도록 반지름에 제곱근을 씀
                float angle = (float)prng.NextDouble() * Mathf.PI * 2f;
                float radius = entry.clusterRadius * Mathf.Sqrt((float)prng.NextDouble());
                position = clusterCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
            else
            {
                position = chunkOrigin + new Vector3(
                    (float)prng.NextDouble() * chunkSize,
                    0f,
                    (float)prng.NextDouble() * chunkSize);
            }

            bool tooClose = false;
            for (int i = 0; i < placedPositions.Count; i++)
            {
                float spacing = Mathf.Max(baseSpacing, placedRadii[i]);
                if ((placedPositions[i] - position).sqrMagnitude < spacing * spacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose) return true;
        }

        position = default;
        return false;
    }
}
