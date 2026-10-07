using UnityEngine;

// 펄린 노이즈 값에 따라 청크의 바이옴을 정함 (BiomeData의 임계값 구간)
[CreateAssetMenu(fileName = "BiomeStep", menuName = "World/Gen/BiomeStep")]
public class BiomeStep : WorldGenStep
{
    [SerializeField] private float noiseScale = 0.1f;

    public override void GenerateChunk(WorldGenContext ctx, Vector2Int coord, ChunkData chunk)
    {
        // 시드마다 노이즈의 다른 구역을 쓰도록 오프셋을 줌
        var prng = ctx.StepRandom(this);
        float offsetX = prng.Next(-100000, 100000);
        float offsetZ = prng.Next(-100000, 100000);

        float noiseValue = Mathf.PerlinNoise(
            coord.x * noiseScale + offsetX,
            coord.y * noiseScale + offsetZ);

        chunk.biome = ctx.BiomeIndex(Resolve(ctx.Biomes, noiseValue));
    }

    private static BiomeData Resolve(BiomeDatabase database, float noiseValue)
    {
        var biomes = database.biomes;
        foreach (var biome in biomes)
        {
            if (noiseValue >= biome.minThreshold && noiseValue < biome.maxThreshold)
                return biome;
        }

        return biomes.Length > 0 ? biomes[0] : null;
    }
}
