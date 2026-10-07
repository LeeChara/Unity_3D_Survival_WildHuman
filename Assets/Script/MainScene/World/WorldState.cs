using System.Collections.Generic;
using UnityEngine;

// 플레이 중인 월드의 맵 데이터 보관과 조회
// 경계 안 청크는 저장된 데이터를, 경계 밖 청크는 같은 생성 단계로 즉석 생성한 데이터를 돌려줌 (경계 밖은 저장하지 않음)
// 다른 시스템이 Awake·Start에서 조회할 수 있도록 먼저 실행
[DefaultExecutionOrder(-100)]
public class WorldState : MonoBehaviour
{
    [SerializeField] private WorldGenConfig config;
    [Tooltip("경계 밖 청크 데이터를 언로드 시 버리기 위해 구독")]
    [SerializeField] private ChunkStreamer chunkStreamer;

    public static WorldState Instance { get; private set; }

    public WorldGenSettings Settings { get; private set; }
    public MapData Map { get; private set; }
    public float ChunkSize => config.grid.chunkSize;

    // 시작·부활 지점 (높이는 사용하는 쪽에서 정함)
    public Vector3 SpawnPoint => new(Map.spawnX, 0f, Map.spawnZ);

    // 경계 안 영역 (x, z)
    public Rect Bounds
    {
        get
        {
            Vector2Int min = Map.MinCoord();
            return new Rect(min.x * ChunkSize, min.y * ChunkSize, Map.width * ChunkSize, Map.height * ChunkSize);
        }
    }

    private WorldGenContext context;
    private readonly Dictionary<Vector2Int, ChunkData> outsideChunks = new();

    private void Awake()
    {
        Instance = this;

        if (WorldSession.HasWorld)
        {
            Settings = WorldSession.Meta.settings;
            Map = WorldSession.Data.map;
        }
        else
        {
            // 에디터에서 MainScene을 바로 실행한 경우
            Settings = config.editorSettings;
            Map = WorldGenerator.Generate(config, Settings);
        }

        context = new WorldGenContext(config, Settings.seed, Map);
    }

    private void OnEnable()
    {
        chunkStreamer.ChunkUnloaded += OnChunkUnloaded;
    }

    private void OnDisable()
    {
        chunkStreamer.ChunkUnloaded -= OnChunkUnloaded;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool IsInside(Vector2Int coord) => Map.Contains(coord);

    public bool IsInside(Vector3 position)
    {
        Rect bounds = Bounds;
        return position.x >= bounds.xMin && position.x < bounds.xMax &&
               position.z >= bounds.yMin && position.z < bounds.yMax;
    }

    // 경계 안쪽으로 margin만큼 들여 보정 (던진 아이템 착지점 등)
    public Vector3 ClampInside(Vector3 position, float margin)
    {
        Rect bounds = Bounds;
        position.x = Mathf.Clamp(position.x, bounds.xMin + margin, bounds.xMax - margin);
        position.z = Mathf.Clamp(position.z, bounds.yMin + margin, bounds.yMax - margin);
        return position;
    }

    public ChunkData GetChunk(Vector2Int coord)
    {
        if (Map.TryGetChunk(coord, out ChunkData chunk)) return chunk;

        if (!outsideChunks.TryGetValue(coord, out chunk))
        {
            chunk = new ChunkData();
            foreach (var step in config.steps)
            {
                step.GenerateChunk(context, coord, chunk);
            }
            outsideChunks[coord] = chunk;
        }
        return chunk;
    }

    public BiomeData GetBiome(Vector2Int coord) => context.GetBiome(GetChunk(coord));

    // 세이브에 있지만 지금은 없는 종류면 null
    public GameObject GetPropPrefab(int type) => context.GetPropPrefab(type);

    private void OnChunkUnloaded(Vector2Int coord)
    {
        outsideChunks.Remove(coord);
    }
}
