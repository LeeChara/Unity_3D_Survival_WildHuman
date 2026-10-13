using System.Collections.Generic;
using UnityEngine;

// 월드 구조물을 청크 로드에 맞춰 배치 (구조물은 중심이 있는 청크에 속함)
// 언로드·저장 시 상태를 MapData의 구조물 기록에 적고, 다시 로드되면 이어서 씀
public class StructureSpawner : MonoBehaviour, ISaveParticipant
{
    [SerializeField] private ChunkStreamer chunkStreamer;
    [SerializeField] private WorldState worldState;

    // 청크 → 그 청크에 중심이 있는 구조물 번호 (MapData.structures 기준)
    private readonly Dictionary<Vector2Int, List<int>> sitesByChunk = new();
    // 구조물 번호 → 배치된 구조물
    private readonly Dictionary<int, WorldStructure> spawned = new();

    private void Awake()
    {
        List<StructureSite> sites = worldState.Map.structures;
        for (int i = 0; i < sites.Count; i++)
        {
            Vector2Int coord = worldState.WorldToChunk(sites[i].Position());
            if (!sitesByChunk.TryGetValue(coord, out List<int> list)) sitesByChunk[coord] = list = new List<int>();
            list.Add(i);
        }
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
        if (!sitesByChunk.TryGetValue(coord, out List<int> indices)) return;

        foreach (int index in indices)
        {
            if (spawned.ContainsKey(index)) continue;

            StructureSite site = worldState.Map.structures[index];
            WorldStructureData data = worldState.GetStructure(site);
            if (data == null || data.prefab == null) continue;

            WorldStructure instance = Instantiate(data.prefab, site.Position(), Quaternion.Euler(0f, site.rotY, 0f), transform);
            instance.Restore(site.state);
            spawned[index] = instance;
        }
    }

    private void OnChunkUnloaded(Vector2Int coord)
    {
        if (!sitesByChunk.TryGetValue(coord, out List<int> indices)) return;

        foreach (int index in indices)
        {
            if (!spawned.TryGetValue(index, out WorldStructure instance)) continue;

            worldState.Map.structures[index].state = instance.Capture();
            spawned.Remove(index);
            Destroy(instance.gameObject);
        }
    }

    // 저장용 복사본(data.map)에만 적음 (배치된 동안은 구조물 오브젝트가 상태를 가짐)
    public void Capture(WorldSaveData data)
    {
        foreach (var (index, instance) in spawned)
        {
            if (instance != null && index < data.map.structures.Count) data.map.structures[index].state = instance.Capture();
        }
    }

    // 구조물 상태는 맵 데이터와 함께 이미 불러와져 있음
    public void Restore(WorldSaveData data) { }
}
