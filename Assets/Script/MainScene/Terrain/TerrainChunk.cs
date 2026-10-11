using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class TerrainChunk : MonoBehaviour
{
    public Vector2Int chunkCoord {  get; private set; }

    private static readonly int FloorTextureId = Shader.PropertyToID("_FloorTexture");
    private static readonly int TilingScaleId = Shader.PropertyToID("_TilingScale");

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    public void Init(Vector2Int chunkCoord)
    {
        this.chunkCoord = chunkCoord;
    }

    // 청크마다 자기 바이옴의 바닥 텍스처로 교체 (머티리얼은 공유하고 MPB로 값만 덮어씀)
    // 경계는 청크 단위로 끊김, 이웃 바이옴은 이후 경계 블렌딩에 사용
    public void ApplyBiome(BiomeData mine, NeighborBiomes neighbors)
    {
        // 바이옴이 없으면 머티리얼 기본값으로 그림
        if (mine == null || mine.planeTexture == null)
        {
            meshRenderer.SetPropertyBlock(null);
            return;
        }

        propertyBlock.Clear();
        propertyBlock.SetTexture(FloorTextureId, mine.planeTexture);
        propertyBlock.SetFloat(TilingScaleId, mine.tilingScale);
        meshRenderer.SetPropertyBlock(propertyBlock);
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
