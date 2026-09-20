using UnityEngine;

public class BiomeGridGenerator : MonoBehaviour
{
    [SerializeField] private BiomeGridSetting setting;
    [SerializeField] private BiomeData[] biomes;

    private BiomeData[,] biomeGrid;
    private float seedOffsetX;
    private float seedOffsetZ;

    private void Awake()
    {
        GenerateGrid();
    }

    private void GenerateGrid()
    {
        var prng = new System.Random(setting.seed);
        seedOffsetX = prng.Next(-100000, 100000);
        seedOffsetZ = prng.Next(-100000, 100000);

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
        foreach (var biome in biomes)
        {
            if (noiseValue >= biome.minThreshold && noiseValue < biome.maxThreshold)
                return biome;
        }

        return biomes.Length > 0 ? biomes[0] : null;
    }

    public BiomeData GetBiome(Vector2Int chunkCoord)
    {
        if (chunkCoord.x < 0 || chunkCoord.x >= setting.mapChunkWidth ||
            chunkCoord.y < 0 || chunkCoord.y >= setting.mapChunkHeight)
        {
            return null;
        }

        return biomeGrid[chunkCoord.x, chunkCoord.y];
    }
}
