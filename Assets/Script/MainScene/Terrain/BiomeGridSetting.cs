using UnityEngine;

// 지형 격자 공통 값 (시드·맵 크기는 월드마다 다르므로 WorldGenSettings에, 생성 규칙은 생성 단계 에셋에 둠)
[CreateAssetMenu(fileName = "BiomeGridSetting", menuName = "Biome/BiomeGridSetting")]
public class BiomeGridSetting : ScriptableObject
{
    [Header("청크 크기")]
    // 청크 하나의 프리팹 스케일
    public float chunkSize = 10f;
}
