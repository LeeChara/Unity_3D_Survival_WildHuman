using UnityEngine;

// 펄린 노이즈 값에 따라 청크의 바이옴을 정함 (BiomeData의 임계값 구간)
[CreateAssetMenu(fileName = "BiomeStep", menuName = "World/Gen/BiomeStep")]
public class BiomeStep : WorldGenStep
{
    [SerializeField] private float noiseScale = 0.1f;

    public override void GenerateChunk(WorldGenContext ctx, Vector2Int coord, ChunkData chunk)
    {
        chunk.biome = ctx.BiomeIndex(Evaluate(ctx, coord));
    }

    // 청크 데이터 없이 좌표만으로 바이옴을 계산 (아직 생성되지 않은 이웃 청크를 보는 단계가 사용)
    public BiomeData Evaluate(WorldGenContext ctx, Vector2Int coord)
    {
        // 시드마다 노이즈의 다른 구역을 쓰도록 오프셋을 줌
        var prng = ctx.StepRandom(this);
        float offsetX = prng.Next(-100000, 100000);
        float offsetZ = prng.Next(-100000, 100000);

        float noiseValue = Mathf.PerlinNoise(
            coord.x * noiseScale + offsetX,
            coord.y * noiseScale + offsetZ);

        return Resolve(ctx.Biomes, noiseValue);
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
