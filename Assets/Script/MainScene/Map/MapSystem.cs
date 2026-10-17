using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// 지도 데이터: 경계 안 지형 텍스처(바이옴·호수), 탐색 안개, 지도에 찍을 점 목록
// 지형은 월드를 불러올 때 MapData로 한 번 굽고, 안개는 플레이어 주변을 칸 단위로 밝혀 가며 저장
// 화면 표시는 MapView(미니맵·전체 지도)가 이 텍스처와 점 목록을 가져다 씀
public class MapSystem : MonoBehaviour, ISaveParticipant
{
    private const string SaveKey = "map";

    [SerializeField] private MapSetting setting;
    [SerializeField] private WorldState worldState;
    [SerializeField] private Transform player;

    public static MapSystem Instance { get; private set; }

    public struct Marker
    {
        public Vector3 position;
        public Color color;
    }

    public MapSetting Setting => setting;
    public Texture2D TerrainTexture { get; private set; }
    public Texture2D FogTexture { get; private set; }
    // 경계 안 영역 (x, z) — 지형·안개 텍스처가 덮는 범위
    public Rect Bounds { get; private set; }
    public Vector3 PlayerPosition => player.position;
    public Vector3 SpawnPoint => worldState.SpawnPoint;

    // 탐색 칸 (인덱스 = x + z * fogWidth)
    private bool[] explored;
    private Color32[] fogPixels;
    private int fogWidth;
    private int fogHeight;
    private bool fogDirty;
    private float revealTimer;
    private bool initialized;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (TerrainTexture != null) Destroy(TerrainTexture);
        if (FogTexture != null) Destroy(FogTexture);
    }

    private void Start()
    {
        EnsureInitialized();
    }

    // 저장 불러오기(WorldSaveManager.Start)나 MapView.Start가 이 Start보다 먼저 올 수도 있어서 어느 쪽에서든 준비
    // (WorldState.Map이 Awake에서 정해지므로 Start 이후에만 부를 것)
    public void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        Bounds = worldState.Bounds;
        BakeTerrain();
        CreateFog();
    }

    private void Update()
    {
        revealTimer -= Time.deltaTime;
        if (revealTimer <= 0f)
        {
            revealTimer = setting.revealInterval;
            Reveal(player.position, setting.revealRadius);
        }

        if (fogDirty)
        {
            fogDirty = false;
            FogTexture.SetPixels32(fogPixels);
            FogTexture.Apply(false);
        }
    }

    #region 지형

    private void BakeTerrain()
    {
        MapData map = worldState.Map;
        float chunkSize = worldState.ChunkSize;
        int width = Mathf.Max(1, Mathf.CeilToInt(Bounds.width / setting.metersPerPixel));
        int height = Mathf.Max(1, Mathf.CeilToInt(Bounds.height / setting.metersPerPixel));

        // 청크별 바이옴 색 (경계 밖은 가장자리 청크 색을 이어 씀)
        var chunkColors = new Color[map.width * map.height];
        Vector2Int min = map.MinCoord();
        for (int z = 0; z < map.height; z++)
        {
            for (int x = 0; x < map.width; x++)
            {
                BiomeData biome = worldState.GetBiome(new Vector2Int(min.x + x, min.y + z));
                chunkColors[x + z * map.width] = biome != null ? biome.mapColor : setting.outsideColor;
            }
        }

        var pixels = new Color32[width * height];
        float blend = Mathf.Min(setting.biomeBlend, chunkSize * 0.5f);
        Span<float> wx = stackalloc float[3];
        Span<float> wz = stackalloc float[3];
        for (int py = 0; py < height; py++)
        {
            float localZ = (py + 0.5f) * setting.metersPerPixel;
            int cz = Mathf.Clamp(Mathf.FloorToInt(localZ / chunkSize), 0, map.height - 1);
            BlendWeights(localZ - cz * chunkSize, chunkSize, blend, wz);

            for (int px = 0; px < width; px++)
            {
                float localX = (px + 0.5f) * setting.metersPerPixel;
                int cx = Mathf.Clamp(Mathf.FloorToInt(localX / chunkSize), 0, map.width - 1);
                BlendWeights(localX - cx * chunkSize, chunkSize, blend, wx);

                // 점 주변 정사각형(반폭 blend)이 겹치는 청크들의 넓이 비율로 섞음
                Color color = Color.clear;
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (wz[dz + 1] <= 0f) continue;
                    int z = Mathf.Clamp(cz + dz, 0, map.height - 1);
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        float weight = wx[dx + 1] * wz[dz + 1];
                        if (weight <= 0f) continue;
                        int x = Mathf.Clamp(cx + dx, 0, map.width - 1);
                        color += chunkColors[x + z * map.width] * weight;
                    }
                }
                color.a = 1f;
                pixels[px + py * width] = color;
            }
        }

        PaintLakes(pixels, width, height);

        TerrainTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "MapTerrain",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        TerrainTexture.SetPixels32(pixels);
        TerrainTexture.Apply(false, true);
    }

    // 청크 안 위치 local에서 반폭 blend 구간이 이전·현재·다음 청크와 겹치는 비율
    private static void BlendWeights(float local, float chunkSize, float blend, Span<float> weights)
    {
        if (blend <= 0f)
        {
            weights[0] = 0f;
            weights[1] = 1f;
            weights[2] = 0f;
            return;
        }

        float span = blend * 2f;
        weights[0] = Mathf.Max(0f, blend - local) / span;
        weights[2] = Mathf.Max(0f, local + blend - chunkSize) / span;
        weights[1] = 1f - weights[0] - weights[2];
    }

    // 호수는 자원 목록에 들어 있으므로 청크마다 호수 종류만 골라 원 배치(Layout)대로 칠함
    private void PaintLakes(Color32[] pixels, int width, int height)
    {
        MapData map = worldState.Map;
        var lakeByType = new Dictionary<int, TileLake>();
        var blobs = new List<Vector4>();
        float mpp = setting.metersPerPixel;

        foreach (ChunkData chunk in map.chunks)
        {
            for (int i = 0; i < chunk.types.Count; i++)
            {
                int type = chunk.types[i];
                if (!lakeByType.TryGetValue(type, out TileLake lake))
                {
                    GameObject prefab = worldState.GetPropPrefab(type);
                    lake = prefab != null ? prefab.GetComponent<TileLake>() : null;
                    lakeByType[type] = lake;
                }
                if (lake == null) continue;

                var center = new Vector3(chunk.px[i], 0f, chunk.pz[i]);
                float bound = lake.Layout(center, blobs, out _);
                Color32 color = lake is LavaLake ? setting.lavaColor : setting.waterColor;

                int x0 = Mathf.Max(0, Mathf.FloorToInt((center.x - bound - Bounds.xMin) / mpp));
                int x1 = Mathf.Min(width - 1, Mathf.CeilToInt((center.x + bound - Bounds.xMin) / mpp));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((center.z - bound - Bounds.yMin) / mpp));
                int y1 = Mathf.Min(height - 1, Mathf.CeilToInt((center.z + bound - Bounds.yMin) / mpp));
                for (int py = y0; py <= y1; py++)
                {
                    float wz = Bounds.yMin + (py + 0.5f) * mpp - center.z;
                    for (int px = x0; px <= x1; px++)
                    {
                        float wx = Bounds.xMin + (px + 0.5f) * mpp - center.x;
                        foreach (Vector4 blob in blobs)
                        {
                            float ox = wx - blob.x;
                            float oz = wz - blob.z;
                            if (ox * ox + oz * oz > blob.w * blob.w) continue;
                            pixels[px + py * width] = color;
                            break;
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region 탐색 안개

    private void CreateFog()
    {
        fogWidth = Mathf.Max(1, Mathf.CeilToInt(Bounds.width / setting.fogCellSize));
        fogHeight = Mathf.Max(1, Mathf.CeilToInt(Bounds.height / setting.fogCellSize));
        explored = new bool[fogWidth * fogHeight];
        fogPixels = new Color32[explored.Length];

        FogTexture = new Texture2D(fogWidth, fogHeight, TextureFormat.RGBA32, false)
        {
            name = "MapFog",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        RefreshFogPixels();
    }

    private void RefreshFogPixels()
    {
        for (int i = 0; i < explored.Length; i++)
        {
            fogPixels[i] = explored[i] ? new Color32(255, 255, 255, 0) : new Color32(255, 255, 255, 255);
        }
        fogDirty = true;
    }

    private void Reveal(Vector3 position, float radius)
    {
        float cell = setting.fogCellSize;
        int cx0 = Mathf.Max(0, Mathf.FloorToInt((position.x - radius - Bounds.xMin) / cell));
        int cx1 = Mathf.Min(fogWidth - 1, Mathf.FloorToInt((position.x + radius - Bounds.xMin) / cell));
        int cz0 = Mathf.Max(0, Mathf.FloorToInt((position.z - radius - Bounds.yMin) / cell));
        int cz1 = Mathf.Min(fogHeight - 1, Mathf.FloorToInt((position.z + radius - Bounds.yMin) / cell));
        float radiusSq = radius * radius;

        for (int z = cz0; z <= cz1; z++)
        {
            float dz = Bounds.yMin + (z + 0.5f) * cell - position.z;
            for (int x = cx0; x <= cx1; x++)
            {
                int index = x + z * fogWidth;
                if (explored[index]) continue;

                float dx = Bounds.xMin + (x + 0.5f) * cell - position.x;
                if (dx * dx + dz * dz > radiusSq) continue;

                explored[index] = true;
                fogPixels[index] = new Color32(255, 255, 255, 0);
                fogDirty = true;
            }
        }
    }

    // 경계 밖은 항상 탐색되지 않은 것으로 봄
    public bool IsExplored(Vector3 position)
    {
        if (explored == null) return false;

        int x = Mathf.FloorToInt((position.x - Bounds.xMin) / setting.fogCellSize);
        int z = Mathf.FloorToInt((position.z - Bounds.yMin) / setting.fogCellSize);
        if (x < 0 || z < 0 || x >= fogWidth || z >= fogHeight) return false;
        return explored[x + z * fogWidth];
    }

    #endregion

    #region 점

    // 월드 구조물(탐색한 곳만)과 플레이어 설치물
    public void GetMarkers(List<Marker> result)
    {
        result.Clear();

        foreach (StructureSite site in worldState.Map.structures)
        {
            WorldStructureData data = worldState.GetStructure(site);
            if (data == null || data.mapColor.a <= 0f) continue;

            Vector3 position = site.Position();
            if (!IsExplored(position)) continue;
            result.Add(new Marker { position = position, color = data.mapColor });
        }

        PlacementManager placement = PlacementManager.Instance;
        if (placement == null) return;
        foreach (StructureHealth structure in placement.Placed)
        {
            if (structure == null) continue;
            Color color = structure.SourceItem.mapColor;
            if (color.a <= 0f) continue;
            result.Add(new Marker { position = structure.transform.position, color = color });
        }
    }

    #endregion

    #region 저장

    public void Capture(WorldSaveData data)
    {
        if (explored == null) return;

        var bytes = new byte[(explored.Length + 7) / 8];
        for (int i = 0; i < explored.Length; i++)
        {
            if (explored[i]) bytes[i >> 3] |= (byte)(1 << (i & 7));
        }
        data.systems[SaveKey] = new JObject
        {
            ["w"] = fogWidth,
            ["h"] = fogHeight,
            ["explored"] = Convert.ToBase64String(bytes),
        };
    }

    public void Restore(WorldSaveData data)
    {
        EnsureInitialized();

        JToken saved = data.GetSystem(SaveKey);
        if (saved == null) return;
        // 칸 크기 설정이 바뀌어 크기가 다르면 기록을 버림
        if (saved.Value<int>("w") != fogWidth || saved.Value<int>("h") != fogHeight) return;

        byte[] bytes = Convert.FromBase64String(saved.Value<string>("explored"));
        for (int i = 0; i < explored.Length && (i >> 3) < bytes.Length; i++)
        {
            explored[i] = (bytes[i >> 3] & (1 << (i & 7))) != 0;
        }
        RefreshFogPixels();
    }

    #endregion
}
