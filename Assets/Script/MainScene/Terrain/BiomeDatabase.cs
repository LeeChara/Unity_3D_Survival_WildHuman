using UnityEngine;

// 바이옴 목록 데이터베이스
// 여러 스크립트에서 해당 에셋을 참조하여
// 인덱스가 어긋나는 것을 방지.
[CreateAssetMenu(fileName = "BiomeDatabase", menuName = "Biome/BiomeDatabase")]
public class BiomeDatabase : ScriptableObject
{
    public BiomeData[] biomes;
}
