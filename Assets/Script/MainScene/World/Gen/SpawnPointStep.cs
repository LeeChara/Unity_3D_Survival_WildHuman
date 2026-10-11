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
        Vector2Int center = ctx.WorldToChunk(position);
        // 여러 청크에 걸친 용암 호수까지 검사하도록 가장 큰 점유 반경만큼 주변 청크를 봄
        int range = Mathf.CeilToInt(PropFootprint.MaxRadius / ctx.ChunkSize);

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dz = -range; dz <= range; dz++)
            {
                if (!ctx.map.TryGetChunk(center + new Vector2Int(dx, dz), out ChunkData chunk)) continue;

                for (int i = 0; i < chunk.PropCount(); i++)
                {
                    var prop = new Vector3(chunk.px[i], 0f, chunk.pz[i]);
                    float limit = clearance + PropFootprint.RadiusOf(ctx.GetPropPrefab(chunk.types[i]), prop);
                    if ((prop - position).sqrMagnitude < limit * limit) return false;
                }
            }
        }
        return true;
    }
}
