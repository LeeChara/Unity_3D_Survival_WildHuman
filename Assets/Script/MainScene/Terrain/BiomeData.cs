using UnityEngine;

[CreateAssetMenu(fileName = "BiomeData", menuName = "Biome/BiomeData")]
public class BiomeData : ScriptableObject
{
    public string biomeName;

    [Header("바닥 텍스처")]
    public Texture2D planeTexture;
    public float tilingScale = 1f;
    public float blendRange = 1f;

    [Header("바이옴 판정 임계값")]
    [Range(0f, 1f)] public float minThreshold;
    [Range(0f, 1f)] public float maxThreshold;

    [Header("스폰 테이블")]
    // TODO: 아직 스폰 로직 구현 전이라 데이터 형식만 정의한 상태
    public PropSpawnEntry[] propSpawnTable;
    public MonsterSpawnEntry[] monsterSpawnTable;
}

[System.Serializable]
public struct PropSpawnEntry
{
    public GameObject propPrefab;
    [Range(0f, 1f)] public float spawnRate;
}

[System.Serializable]
public struct MonsterSpawnEntry
{
    public MonsterData monster;
    [Range(0f, 1f)] public float spawnRate;
}