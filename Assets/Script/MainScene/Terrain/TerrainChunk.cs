using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class TerrainChunk : MonoBehaviour
{
    public Vector2Int chunkCoord {  get; private set; }

    private MeshRenderer meshRenderer;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    public void Init(Vector2Int chunkCoord)
    {
        this.chunkCoord = chunkCoord;
    }

    public void ApplyBiome(BiomeData mine, NeighborBiomes neighbors)
    {
        // TODO: MPB로 바이옴 값 주입
    }
}

[System.Serializable]
public struct NeighborBiomes
{
    public BiomeData East;
    public BiomeData West;
    public BiomeData South;
    public BiomeData North;
}
