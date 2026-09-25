using System;
using System.Collections.Generic;
using UnityEngine;

public class ChunkStreamer : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private BiomeGridSetting setting;
    [SerializeField] private BiomeGridGenerator biomeGrid;
    [SerializeField] private TerrainChunk chunkPrefab;
    [SerializeField] private int loadRadius = 3;

    private readonly Dictionary<Vector2Int, TerrainChunk> activeChunks = new();
    private readonly Queue<TerrainChunk> pool = new();

    // 청크 로드/언로드 시점을 스포너 등 외부 시스템에 알림
    // 맵 범위 밖 청크는 biome이 null로 전달됨
    public event Action<Vector2Int, BiomeData> ChunkLoaded;
    public event Action<Vector2Int> ChunkUnloaded;

    private Vector2Int currentPlayerChunk;
    private bool initialized;

    private void Awake()
    {
        int poolSize = (loadRadius * 2 + 1) * (loadRadius * 2 + 1);
        for (int i = 0; i < poolSize; i++)
        {
            TerrainChunk chunk = Instantiate(chunkPrefab, transform);
            chunk.gameObject.SetActive(false);
            pool.Enqueue(chunk);
        }
    }

    private void Update()
    {
        Vector2Int playerChunk = WorldToChunkCoord(player.position);

        if (!initialized || playerChunk != currentPlayerChunk)
        {
            currentPlayerChunk = playerChunk;
            initialized = true;
            UpdateActiveChunks();
        }
    }

    private Vector2Int WorldToChunkCoord(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x / setting.chunkSize);
        int z = Mathf.FloorToInt(worldPos.z / setting.chunkSize);
        return new Vector2Int(x, z);
    }

    private void UpdateActiveChunks()
    {
        var shouldBeActive = new HashSet<Vector2Int>();
        for (int dx = -loadRadius; dx <= loadRadius; dx++)
        {
            for (int dz = -loadRadius; dz <= loadRadius; dz++)
            {
                shouldBeActive.Add(currentPlayerChunk + new Vector2Int(dx, dz));
            }
        }

        var toRelease = new List<Vector2Int>();
        foreach (var coord in activeChunks.Keys)
        {
            if (!shouldBeActive.Contains(coord))
                toRelease.Add(coord);
        }
        foreach (var coord in toRelease)
        {
            ReleaseChunk(coord);
        }

        foreach (var coord in shouldBeActive)
        {
            if (!activeChunks.ContainsKey(coord))
                SpawnChunk(coord);
        }
    }

    private void SpawnChunk(Vector2Int coord)
    {
        TerrainChunk chunk = pool.Dequeue();

        float worldX = (coord.x + 0.5f) * setting.chunkSize;
        float worldZ = (coord.y + 0.5f) * setting.chunkSize;
        chunk.transform.position = new Vector3(worldX, 0f, worldZ);
        chunk.gameObject.SetActive(true);
        chunk.Init(coord);

        activeChunks[coord] = chunk;

        ChunkLoaded?.Invoke(coord, biomeGrid.GetBiome(coord));
    }

    private void ReleaseChunk(Vector2Int coord)
    {
        TerrainChunk chunk = activeChunks[coord];
        activeChunks.Remove(coord);
        chunk.gameObject.SetActive(false);
        pool.Enqueue(chunk);

        ChunkUnloaded?.Invoke(coord);
    }
}
