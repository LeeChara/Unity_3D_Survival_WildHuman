using System.Collections.Generic;
using UnityEngine;

// 생성 단계들이 공유하는 정보와 도구 (시드별 난수, 이름표 번호 변환, 좌표 변환)
// 월드 생성 중에도, 플레이 중 경계 밖 청크 생성·종류 조회에도 같은 객체를 씀
public class WorldGenContext
{
    public readonly WorldGenConfig config;
    public readonly int seed;
    public readonly MapData map;

    private readonly Dictionary<string, BiomeData> biomesById = new();
    private readonly Dictionary<string, GameObject> propsById = new();

    // 이름표 번호 → 에셋 (세이브를 읽을 때 번호마다 한 번만 찾도록 캐시)
    private readonly List<BiomeData> biomeCache = new();
    private readonly List<GameObject> propCache = new();

    public float ChunkSize => config.grid.chunkSize;
    public BiomeDatabase Biomes => config.biomeDatabase;

    public WorldGenContext(WorldGenConfig config, int seed, MapData map)
    {
        this.config = config;
        this.seed = seed;
        this.map = map;

        foreach (var biome in config.biomeDatabase.biomes)
        {
            if (biome == null) continue;
            biomesById[biome.id] = biome;

            if (biome.propSpawnTable == null) continue;
            foreach (var entry in biome.propSpawnTable)
            {
                if (entry.propPrefab != null) propsById[PropId(entry.propPrefab)] = entry.propPrefab;
            }
        }
    }

    // 자원 프리팹의 저장용 id (PropData.id, 없으면 프리팹 이름)
    public static string PropId(GameObject prefab)
    {
        if (prefab.TryGetComponent(out PropHealth health) && health.Data != null && !string.IsNullOrEmpty(health.Data.id))
            return health.Data.id;
        return prefab.name;
    }

    #region 난수

    // 단계마다 다른 시드를 써서, 단계를 추가·변경해도 다른 단계의 결과가 밀리지 않음
    public int StepSeed(WorldGenStep step) => seed ^ StableHash(step.Key);

    public System.Random StepRandom(WorldGenStep step) => new(StepSeed(step));

    // 청크마다 독립된 난수 (생성 순서와 관계없이 같은 결과)
    // Vector2Int.GetHashCode()는 인접 좌표끼리 충돌이 잦아 소수 곱 기반 해시 사용
    public System.Random ChunkRandom(WorldGenStep step, Vector2Int coord)
    {
        unchecked
        {
            return new System.Random(StepSeed(step) ^ (coord.x * 73856093) ^ (coord.y * 19349663));
        }
    }

    // string.GetHashCode는 실행 환경마다 달라질 수 있어 직접 계산 (FNV-1a)
    public static int StableHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)hash;
        }
    }

    #endregion

    #region 이름표

    public int BiomeIndex(BiomeData biome) => biome == null ? -1 : PaletteIndex(map.biomePalette, biome.id);

    public int PropIndex(GameObject prefab) => PaletteIndex(map.propPalette, PropId(prefab));

    // 세이브에 있지만 지금은 없는 종류(삭제된 에셋 등)는 null
    public BiomeData GetBiome(ChunkData chunk)
    {
        if (chunk == null || chunk.biome < 0) return null;
        return Resolve(chunk.biome, map.biomePalette, biomesById, biomeCache);
    }

    public GameObject GetPropPrefab(int type) => Resolve(type, map.propPalette, propsById, propCache);

    private static int PaletteIndex(List<string> palette, string id)
    {
        int index = palette.IndexOf(id);
        if (index >= 0) return index;

        palette.Add(id);
        return palette.Count - 1;
    }

    private static T Resolve<T>(int index, List<string> palette, Dictionary<string, T> byId, List<T> cache) where T : Object
    {
        if (index < 0 || index >= palette.Count) return null;

        // 경계 밖 생성으로 이름표가 늘어날 수 있어 길이를 맞춰 가며 캐시
        while (cache.Count < palette.Count)
        {
            string id = palette[cache.Count];
            if (!byId.TryGetValue(id, out T found)) Debug.LogWarning($"[WorldGenContext] 세이브의 '{id}'에 해당하는 에셋이 없음");
            cache.Add(found);
        }
        return cache[index];
    }

    #endregion

    public Vector2Int WorldToChunk(Vector3 position)
    {
        return new Vector2Int(Mathf.FloorToInt(position.x / ChunkSize), Mathf.FloorToInt(position.z / ChunkSize));
    }

    public Vector3 ChunkOrigin(Vector2Int coord)
    {
        return new Vector3(coord.x * ChunkSize, 0f, coord.y * ChunkSize);
    }
}
