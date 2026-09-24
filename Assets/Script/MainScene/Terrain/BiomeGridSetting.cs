using UnityEngine;

[CreateAssetMenu(fileName = "BiomeGridSetting", menuName = "Biome/BiomeGridSetting")]
public class BiomeGridSetting : ScriptableObject
{
    [Header("시드")]
    public int seed;

    [Header("노이즈")]
    public float noiseScale = 0.1f;

    [Header("맵 크기")]
    public int mapChunkWidth = 32;
    public int mapChunkHeight = 32;

    [Header("청크 크기")]
    // 청크 하나의 프리팹 스케일
    public float chunkSize = 10f;
}
