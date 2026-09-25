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
    public PropSpawnEntry[] propSpawnTable;
    // TODO: 아직 몬스터 스폰 로직 구현 전이라 데이터 형식만 정의한 상태
    public MonsterSpawnEntry[] monsterSpawnTable;
}

[System.Serializable]
public struct PropSpawnEntry
{
    public GameObject propPrefab;

    // 청크당 스폰 개수 (min~max 사이에서 결정)
    [Min(0)] public int minCount;
    [Min(0)] public int maxCount;

    // 같은 청크에 이미 배치된 Prop과의 최소 거리
    [Min(0f)] public float minSpacing;
}

[System.Serializable]
public struct MonsterSpawnEntry
{
    public MonsterData monster;
    [Range(0f, 1f)] public float spawnRate;
}