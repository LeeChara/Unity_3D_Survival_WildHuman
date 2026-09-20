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
}
