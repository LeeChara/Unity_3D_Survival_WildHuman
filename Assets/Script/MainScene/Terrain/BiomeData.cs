using UnityEngine;

[CreateAssetMenu(fileName = "BiomeData", menuName = "Biome/BiomeData")]
public class BiomeData : ScriptableObject
{
    [Tooltip("저장·로드 시 식별용 고유 ID (한 번 정하면 변경하지 않음)")]
    public string id;
    public string biomeName;
    [Tooltip("도감에 표시할 설명")]
    [TextArea] public string description;

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

    [Tooltip("비어 있지 않으면 min~max 대신 이 가중치로 청크당 개수를 정함 (가중치는 상대값)")]
    public PropCountWeight[] countWeights;

    // 같은 청크에 이미 배치된 Prop과의 최소 거리
    [Min(0f)] public float minSpacing;

    [Tooltip("0보다 크면 청크 안 한 지점을 중심으로 이 반경 안에 몰아서 배치")]
    [Min(0f)] public float clusterRadius;

    private bool HasWeights => countWeights != null && countWeights.Length > 0;

    // 항목당 난수를 1번만 소비 (월드 생성 결정론 유지)
    public int RollCount(System.Random prng)
    {
        if (!HasWeights)
        {
            return prng.Next(minCount, Mathf.Max(minCount, maxCount) + 1);
        }

        double roll = prng.NextDouble() * TotalWeight();
        foreach (var w in countWeights)
        {
            if (w.weight <= 0f) continue;
            roll -= w.weight;
            if (roll < 0) return w.count;
        }
        return 0;
    }

    // 출현했을 때의 개수 범위 (0개와 가중치 0인 항목 제외, 출현 확률은 AppearChance로 따로 표시)
    public void GetCountRange(out int min, out int max)
    {
        if (!HasWeights)
        {
            max = Mathf.Max(minCount, maxCount);
            min = Mathf.Min(Mathf.Max(minCount, 1), max);
            return;
        }

        min = int.MaxValue;
        max = 0;
        foreach (var w in countWeights)
        {
            if (w.weight <= 0f || w.count == 0) continue;
            min = Mathf.Min(min, w.count);
            max = Mathf.Max(max, w.count);
        }
        if (min == int.MaxValue) min = 0;
    }

    // 청크에 1개 이상 나올 확률
    public float AppearChance()
    {
        if (!HasWeights) return 1f - ZeroChanceUniform();

        float total = TotalWeight();
        if (total <= 0f) return 0f;

        float zero = 0f;
        foreach (var w in countWeights)
        {
            if (w.count == 0 && w.weight > 0f) zero += w.weight;
        }
        return 1f - zero / total;
    }

    private float ZeroChanceUniform() => minCount > 0 ? 0f : 1f / (Mathf.Max(minCount, maxCount) + 1);

    private float TotalWeight()
    {
        float total = 0f;
        foreach (var w in countWeights)
        {
            if (w.weight > 0f) total += w.weight;
        }
        return total;
    }
}

[System.Serializable]
public struct PropCountWeight
{
    [Min(0)] public int count;
    [Min(0f)] public float weight;
}

[System.Serializable]
public struct MonsterSpawnEntry
{
    public MonsterAI monsterPrefab;

    // 같은 바이옴 테이블 안에서의 상대 가중치
    [Min(0f)] public float weight;
}