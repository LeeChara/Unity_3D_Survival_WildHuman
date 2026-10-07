using UnityEngine;

[CreateAssetMenu(fileName = "BiomeData", menuName = "Biome/BiomeData")]
public class BiomeData : ScriptableObject
{
    [Tooltip("저장·로드 시 식별용 고유 ID (한 번 정하면 변경하지 않음)")]
    public string id;
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
    public MonsterAI monsterPrefab;

    // 같은 바이옴 테이블 안에서의 상대 가중치
    [Min(0f)] public float weight;
}