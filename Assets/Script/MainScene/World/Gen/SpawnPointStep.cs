using UnityEngine;

// 맵 중앙 근처에서 자원과 겹치지 않는 시작 지점을 정함
[CreateAssetMenu(fileName = "SpawnPointStep", menuName = "World/Gen/SpawnPointStep")]
public class SpawnPointStep : WorldGenStep
{
    [Tooltip("시작 지점과 자원 사이 최소 거리")]
    [SerializeField] private float clearance = 3f;
    [Tooltip("중앙이 막혀 있을 때 찾아볼 반경")]
    [SerializeField] private float searchRadius = 30f;
    [SerializeField] private int maxAttempts = 50;

    public override void GenerateMap(WorldGenContext ctx)
    {
        var prng = ctx.StepRandom(this);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // 첫 시도는 정중앙
            Vector3 candidate = Vector3.zero;
            if (attempt > 0)
            {
                float angle = (float)prng.NextDouble() * Mathf.PI * 2f;
                float distance = Mathf.Sqrt((float)prng.NextDouble()) * searchRadius;
                candidate = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            }

            if (!IsClear(ctx, candidate)) continue;

            ctx.map.spawnX = candidate.x;
            ctx.map.spawnZ = candidate.z;
            return;
        }

        // 다 막혀 있으면 정중앙 (자원과 겹쳐도 밀려나므로 진행은 가능)
        ctx.map.spawnX = 0f;
        ctx.map.spawnZ = 0f;
    }

    private bool IsClear(WorldGenContext ctx, Vector3 position)
    {
        float clearanceSqr = clearance * clearance;
        Vector2Int center = ctx.WorldToChunk(position);

        // 청크 경계 근처 자원도 검사하도록 주변 9개 청크
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!ctx.map.TryGetChunk(center + new Vector2Int(dx, dz), out ChunkData chunk)) continue;

                for (int i = 0; i < chunk.PropCount(); i++)
                {
                    float x = chunk.px[i] - position.x;
                    float z = chunk.pz[i] - position.z;
                    if (x * x + z * z < clearanceSqr) return false;
                }
            }
        }
        return true;
    }
}
