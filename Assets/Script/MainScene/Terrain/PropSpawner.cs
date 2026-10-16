using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// 청크 로드 시 WorldState의 청크 데이터에 적힌 자원을 배치
// 경계 안은 월드 생성 때 정해져 저장된 목록, 경계 밖은 시드로 즉석 생성한 목록
// 파괴된 자원은 청크 데이터에서도 지우므로 다시 불러와도 되살아나지 않음 (경계 밖은 언로드 시 버려져 원래대로)
// 플레이 중 자원 추가·종류 변경(재생, 지진)도 여기서 청크 데이터와 배치된 오브젝트를 함께 바꿈
// 불타는 자원은 언로드·저장 시 체력과 남은 연소 시간을 청크 데이터에 적고, 다시 로드되면 이어서 탐 (언로드된 동안은 멈춤)
public class PropSpawner : MonoBehaviour, ISaveParticipant
{
    [SerializeField] private ChunkStreamer chunkStreamer;
    [SerializeField] private WorldState worldState;

    // 자원이 파괴되어 청크 데이터에서 지워진 뒤 호출 (청크 좌표, 파괴된 자원, 위치)
    // 자원은 바로 풀에 반환되므로 구독자는 이 호출 안에서만 읽어야 함
    public event Action<Vector2Int, PropHealth, Vector3> PropDepleted;

    // 청크의 자원을 모두 배치한 뒤 호출 (ChunkStreamer.ChunkLoaded 구독 순서와 관계없이 배치 이후임이 보장됨)
    public event Action<Vector2Int> ChunkPropsSpawned;

    // 지금 자원이 배치되어 있는 청크
    public IEnumerable<Vector2Int> LoadedChunks => spawnedProps.Keys;

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
            SpawnedProp prop = Spawn(coord, chunk.types[i], chunk.PropPosition(i));
            if (chunk.TakeBurn(i, out float hp, out float left)) RestoreBurn(prop.instance, hp, left);
            if (chunk.TakeWet(i, out float wetLeft)) RestoreWet(prop.instance, wetLeft);
            props.Add(prop);
        }

        spawnedProps[coord] = props;
        ChunkPropsSpawned?.Invoke(coord);
    }

    // 프리팹을 찾지 못한 종류면 빈 자리 (번호를 맞추기 위해)
    private SpawnedProp Spawn(Vector2Int coord, int type, Vector3 position)
    {
        GameObject prefab = worldState.GetPropPrefab(type);
        if (prefab == null) return default;

        GameObject instance = GetPool(prefab).Get();
        instance.transform.position = position;

        // 풀에서 꺼낼 때(OnEnable) 이전 위치로 계산됐으므로 재계산
        if (instance.TryGetComponent(out DepthSorter sorter)) sorter.Refresh();

        propChunks[instance] = coord;
        return new SpawnedProp { prefab = prefab, instance = instance };
    }

    private void Despawn(SpawnedProp prop)
    {
        if (prop.instance == null) return;

        propChunks.Remove(prop.instance);
        pools[prop.prefab].Release(prop.instance);
    }

    // 청크 데이터에 자원을 추가하고, 청크가 로드되어 있으면 바로 배치. 추가된 번호를 반환
    public int AddProp(Vector2Int coord, GameObject prefab, Vector3 position)
    {
        ChunkData chunk = worldState.GetChunk(coord);
        int type = worldState.GetPropType(prefab);
        int index = chunk.AddProp(type, position.x, position.z);

        if (spawnedProps.TryGetValue(coord, out var props)) props.Add(Spawn(coord, type, chunk.PropPosition(index)));
        return index;
    }

    // 같은 자리의 자원을 다른 종류로 바꿈 (번호 유지)
    public void ReplaceProp(Vector2Int coord, int index, GameObject prefab)
    {
        ChunkData chunk = worldState.GetChunk(coord);
        int type = worldState.GetPropType(prefab);
        chunk.types[index] = type;

        if (!spawnedProps.TryGetValue(coord, out var props)) return;

        Despawn(props[index]);
        props[index] = Spawn(coord, type, chunk.PropPosition(index));
    }

    private void OnChunkUnloaded(Vector2Int coord)
    {
        if (!spawnedProps.TryGetValue(coord, out var props)) return;

        CaptureBurns(props, worldState.GetChunk(coord));
        foreach (var prop in props)
        {
            Despawn(prop);
        }
        spawnedProps.Remove(coord);
    }

    #region 불타는·젖은 자원

    private static void RestoreBurn(GameObject instance, float hp, float left)
    {
        if (instance == null || !instance.TryGetComponent(out PropHealth health) || health.Combustible == null) return;

        health.RestoreHealth(hp);
        health.Combustible.Ignite(left);
    }

    private static void RestoreWet(GameObject instance, float left)
    {
        if (instance != null && instance.TryGetComponent(out Wettable wettable)) wettable.Soak(left);
    }

    // 지금 타고 있거나 젖은 자원의 상태를 청크 데이터에 적음
    private static void CaptureBurns(List<SpawnedProp> props, ChunkData chunk)
    {
        for (int i = 0; i < props.Count; i++)
        {
            GameObject instance = props[i].instance;
            if (instance == null || !instance.TryGetComponent(out PropHealth health) || health.IsDead) continue;

            Combustible combustible = health.Combustible;
            if (combustible != null && combustible.IsBurning) chunk.SetBurn(i, health.CurrentHealth, combustible.Remaining);
            if (instance.TryGetComponent(out Wettable wettable) && wettable.IsWet) chunk.SetWet(i, wettable.Remaining);
        }
    }

    // 저장 데이터의 맵(복사본)에 로드된 청크의 불타는·젖은 자원을 적음 (언로드된 청크는 이미 청크 데이터에 있음)
    public void Capture(WorldSaveData data)
    {
        if (data.map == null) return;

        foreach (var (coord, props) in spawnedProps)
        {
            if (data.map.TryGetChunk(coord, out ChunkData chunk)) CaptureBurns(props, chunk);
        }
    }

    // 맵 데이터와 함께 불러오므로 따로 할 일 없음 (청크가 로드될 때 적용)
    public void Restore(WorldSaveData data) { }

    #endregion

    // 파괴된 자원을 풀에 반환하고 청크 데이터에서 지움 (언로드 시 중복 반환 방지)
    private void OnPropDepleted(PropHealth propHealth)
    {
        GameObject instance = propHealth.gameObject;
        if (!propChunks.TryGetValue(instance, out Vector2Int coord)) return;

        Vector3 position = instance.transform.position;
        List<SpawnedProp> props = spawnedProps[coord];
        int index = props.FindIndex(prop => prop.instance == instance);
        Despawn(props[index]);
        props.RemoveAt(index);
        worldState.GetChunk(coord).RemovePropAt(index);

        PropDepleted?.Invoke(coord, propHealth, position);
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
