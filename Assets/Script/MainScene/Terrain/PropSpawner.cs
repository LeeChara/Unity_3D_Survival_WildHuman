using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// 청크 로드 시 WorldState의 청크 데이터에 적힌 자원을 배치
// 경계 안은 월드 생성 때 정해져 저장된 목록, 경계 밖은 시드로 즉석 생성한 목록
// 파괴된 자원은 청크 데이터에서도 지우므로 다시 불러와도 되살아나지 않음 (경계 밖은 언로드 시 버려져 원래대로)
public class PropSpawner : MonoBehaviour
{
    [SerializeField] private ChunkStreamer chunkStreamer;
    [SerializeField] private WorldState worldState;

    private readonly Dictionary<GameObject, ObjectPool<GameObject>> pools = new();
    // 청크 데이터의 자원 목록과 같은 순서로 유지 (파괴 시 같은 번호를 함께 지움)
    private readonly Dictionary<Vector2Int, List<SpawnedProp>> spawnedProps = new();
    // 파괴된 자원이 어느 청크 소속인지 찾기 위한 역참조
    private readonly Dictionary<GameObject, Vector2Int> propChunks = new();

    private struct SpawnedProp
    {
        public GameObject prefab;
        // 프리팹을 찾지 못한 종류면 null (번호를 맞추기 위해 자리만 차지)
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
        ChunkData chunk = worldState.GetChunk(coord);
        var props = new List<SpawnedProp>(chunk.PropCount());

        for (int i = 0; i < chunk.PropCount(); i++)
        {
            GameObject prefab = worldState.GetPropPrefab(chunk.types[i]);
            if (prefab == null)
            {
                props.Add(default);
                continue;
            }

            GameObject instance = GetPool(prefab).Get();
            instance.transform.position = new Vector3(chunk.px[i], 0f, chunk.pz[i]);

            // 풀에서 꺼낼 때(OnEnable) 이전 위치로 계산됐으므로 재계산
            if (instance.TryGetComponent(out DepthSorter sorter)) sorter.Refresh();

            props.Add(new SpawnedProp { prefab = prefab, instance = instance });
            propChunks[instance] = coord;
        }

        spawnedProps[coord] = props;
    }

    private void OnChunkUnloaded(Vector2Int coord)
    {
        if (!spawnedProps.TryGetValue(coord, out var props)) return;

        foreach (var prop in props)
        {
            if (prop.instance == null) continue;

            propChunks.Remove(prop.instance);
            pools[prop.prefab].Release(prop.instance);
        }
        spawnedProps.Remove(coord);
    }

    // 파괴된 자원을 풀에 반환하고 청크 데이터에서 지움 (언로드 시 중복 반환 방지)
    private void OnPropDepleted(PropHealth propHealth)
    {
        GameObject instance = propHealth.gameObject;
        if (!propChunks.TryGetValue(instance, out Vector2Int coord)) return;

        propChunks.Remove(instance);

        List<SpawnedProp> props = spawnedProps[coord];
        int index = props.FindIndex(prop => prop.instance == instance);
        pools[props[index].prefab].Release(instance);
        props.RemoveAt(index);
        worldState.GetChunk(coord).RemovePropAt(index);
    }

    private ObjectPool<GameObject> GetPool(GameObject prefab)
    {
        if (!pools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    GameObject obj = Instantiate(prefab, transform);
                    // 인스턴스는 풀에서 계속 재사용되므로 생성 시 한 번만 구독
                    if (obj.TryGetComponent(out PropHealth propHealth)) propHealth.Depleted += OnPropDepleted;
                    return obj;
                },
                actionOnGet: obj => obj.SetActive(true),
                actionOnRelease: obj => obj.SetActive(false),
                actionOnDestroy: obj => Destroy(obj));
            pools[prefab] = pool;
        }
        return pool;
    }
}
