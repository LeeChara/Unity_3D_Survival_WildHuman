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

        // 앞 단계가 이미 놓은 것과도 간격을 지킴
        var placedPositions = new List<Vector3>();
        for (int i = 0; i < chunk.PropCount(); i++)
        {
            placedPositions.Add(new Vector3(chunk.px[i], 0f, chunk.pz[i]));
        }

        // 테이블 순서대로 난수를 소비해야 결정론이 유지됨
        foreach (var entry in biome.propSpawnTable)
        {
            if (entry.propPrefab == null) continue;

            int type = ctx.PropIndex(entry.propPrefab);
            int maxCount = Mathf.Max(entry.minCount, entry.maxCount);
            int count = prng.Next(entry.minCount, maxCount + 1);

            for (int i = 0; i < count; i++)
            {
                if (!TryFindPosition(prng, chunkOrigin, ctx.ChunkSize, entry.minSpacing, placedPositions, out Vector3 position))
                    continue;

                // 세이브 크기를 줄이기 위해 소수 둘째 자리까지만 저장
                position.x = Mathf.Round(position.x * 100f) / 100f;
                position.z = Mathf.Round(position.z * 100f) / 100f;

                placedPositions.Add(position);
                chunk.AddProp(type, position.x, position.z);
            }
        }
    }

    private bool TryFindPosition(System.Random prng, Vector3 chunkOrigin, float chunkSize, float minSpacing,
        List<Vector3> placedPositions, out Vector3 position)
    {
        float minSpacingSqr = minSpacing * minSpacing;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            position = chunkOrigin + new Vector3(
                (float)prng.NextDouble() * chunkSize,
                0f,
                (float)prng.NextDouble() * chunkSize);

            bool tooClose = false;
            foreach (var placed in placedPositions)
            {
                if ((placed - position).sqrMagnitude < minSpacingSqr)
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
