using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// 청크 로드 시 바이옴의 propSpawnTable에 따라 Prop을 배치
// 청크 좌표 + 전역 시드 기반 결정론적 랜덤이라 재로드해도 항상 같은 위치에 배치됨
public class PropSpawner : MonoBehaviour
{
    [SerializeField] private ChunkStreamer chunkStreamer;
    [SerializeField] private BiomeGridSetting setting;

    // minSpacing을 만족하는 위치를 찾기 위한 최대 시도 횟수
    [SerializeField] private int maxPlacementAttempts = 10;

    private readonly Dictionary<GameObject, ObjectPool<GameObject>> pools = new();
    private readonly Dictionary<Vector2Int, List<SpawnedProp>> spawnedProps = new();

    private struct SpawnedProp
    {
        public GameObject prefab;
        public GameObject instance;
    }

    private void OnEnable()
    {
        chunkStreamer.ChunkLoaded += OnChunkLoaded;
        chunkStreamer.ChunkUnloaded += OnChunkUnloaded;
    }

    private void OnDisable()
    {
        chunkStreamer.ChunkLoaded -= OnChunkLoaded;
        chunkStreamer.ChunkUnloaded -= OnChunkUnloaded;
    }

    private void OnChunkLoaded(Vector2Int coord, BiomeData biome)
    {
        if (biome == null || biome.propSpawnTable == null) return;

        var prng = new System.Random(GetChunkSeed(coord));
        var props = new List<SpawnedProp>();
        var placedPositions = new List<Vector3>();

        Vector3 chunkOrigin = new Vector3(coord.x * setting.chunkSize, 0f, coord.y * setting.chunkSize);

        // 테이블 순서대로 난수를 소비해야 결정론이 유지됨
        foreach (var entry in biome.propSpawnTable)
        {
            if (entry.propPrefab == null) continue;

            int maxCount = Mathf.Max(entry.minCount, entry.maxCount);
            int count = prng.Next(entry.minCount, maxCount + 1);

            for (int i = 0; i < count; i++)
            {
                if (!TryFindPosition(prng, chunkOrigin, entry.minSpacing, placedPositions, out Vector3 position))
                    continue;

                GameObject instance = GetPool(entry.propPrefab).Get();
                instance.transform.position = position;

                placedPositions.Add(position);
                props.Add(new SpawnedProp { prefab = entry.propPrefab, instance = instance });
            }
        }

        spawnedProps[coord] = props;
    }

    private void OnChunkUnloaded(Vector2Int coord)
    {
        if (!spawnedProps.TryGetValue(coord, out var props)) return;

        foreach (var prop in props)
        {
            pools[prop.prefab].Release(prop.instance);
        }
        spawnedProps.Remove(coord);
    }

    private bool TryFindPosition(System.Random prng, Vector3 chunkOrigin, float minSpacing,
        List<Vector3> placedPositions, out Vector3 position)
    {
        float minSpacingSqr = minSpacing * minSpacing;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            position = chunkOrigin + new Vector3(
                (float)prng.NextDouble() * setting.chunkSize,
                0f,
                (float)prng.NextDouble() * setting.chunkSize);

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

    // Vector2Int.GetHashCode()는 인접 좌표끼리 충돌이 잦아 소수 곱 기반 해시 사용
    private int GetChunkSeed(Vector2Int coord)
    {
        unchecked
        {
            return (coord.x * 73856093) ^ (coord.y * 19349663) ^ setting.seed;
        }
    }

    private ObjectPool<GameObject> GetPool(GameObject prefab)
    {
        if (!pools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(prefab, transform),
                actionOnGet: obj => obj.SetActive(true),
                actionOnRelease: obj => obj.SetActive(false),
                actionOnDestroy: obj => Destroy(obj));
            pools[prefab] = pool;
        }
        return pool;
    }
}
