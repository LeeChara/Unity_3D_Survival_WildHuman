using UnityEngine;

public class BiomeGridGenerator : MonoBehaviour
{
    [SerializeField] private BiomeGridSetting setting;
    [SerializeField] private BiomeDatabase biomeDatabase;

    private BiomeData[,] biomeGrid;
    private float seedOffsetX;
    private float seedOffsetZ;

    // 맵을 원점 중심으로 배치하기 위한 보정값
    // 청크 좌표 + gridOffset = 그리드 배열 인덱스
    private Vector2Int gridOffset;

    private void Awake()
    {
        GenerateGrid();
    }

    private void GenerateGrid()
    {
        var prng = new System.Random(setting.seed);
        seedOffsetX = prng.Next(-100000, 100000);
        seedOffsetZ = prng.Next(-100000, 100000);

        gridOffset = new Vector2Int(setting.mapChunkWidth / 2, setting.mapChunkHeight / 2);
        biomeGrid = new BiomeData[setting.mapChunkWidth, setting.mapChunkHeight];

        for (int x = 0; x < setting.mapChunkWidth; x++)
        {
            for (int z = 0; z < setting.mapChunkHeight; z++)
            {
                float noiseValue = Mathf.PerlinNoise(
                    x * setting.noiseScale + seedOffsetX,
                    z * setting.noiseScale + seedOffsetZ);

                biomeGrid[x, z] = ResolveBiome(noiseValue);
            }
        }
    }

    private BiomeData ResolveBiome(float noiseValue)
    {
        var biomes = biomeDatabase.biomes;
        foreach (var biome in biomes)
        {
            if (noiseValue >= biome.minThreshold && noiseValue < biome.maxThreshold)
                return biome;
        }

        return biomes.Length > 0 ? biomes[0] : null;
    }

    public BiomeData GetBiome(Vector2Int chunkCoord)
    {
        Vector2Int index = chunkCoord + gridOffset;

        if (index.x < 0 || index.x >= setting.mapChunkWidth ||
            index.y < 0 || index.y >= setting.mapChunkHeight)
        {
            return null;
        }

        return biomeGrid[index.x, index.y];
    }
}
