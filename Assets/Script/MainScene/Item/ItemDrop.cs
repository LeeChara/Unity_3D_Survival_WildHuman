using System;
using UnityEngine;
using Random = UnityEngine.Random;

// 드랍 테이블의 한 항목
[Serializable]
public class ItemDrop
{
    public ItemData item;
    [Min(0)] public int minCount = 1;
    [Min(0)] public int maxCount = 1;
    [Tooltip("드랍 확률 (1이면 항상 드랍)")]
    [Range(0f, 1f)] public float chance = 1f;

    [Tooltip("비어 있지 않으면 확률·min~max 대신 이 가중치로 개수를 정함 (가중치는 상대값)")]
    public PropCountWeight[] countWeights;

    public bool HasWeights => countWeights != null && countWeights.Length > 0;

    // 확률과 개수 범위로 이번에 떨어뜨릴 개수를 정함 (확률에 실패하면 0)
    public int Roll()
    {
        if (!HasWeights)
        {
            if (Random.value > chance) return 0;
            return Random.Range(minCount, Mathf.Max(minCount, maxCount) + 1);
        }

        float roll = Random.value * TotalWeight();
        foreach (var w in countWeights)
        {
            if (w.weight <= 0f) continue;
            roll -= w.weight;
            if (roll < 0f) return w.count;
        }
        return 0;
    }

    // 드랍됐을 때의 개수 범위 (0개와 가중치 0인 항목 제외, 드랍 확률은 DropChance로 따로 표시)
    public void GetCountRange(out int min, out int max)
    {
        if (!HasWeights)
        {
            min = minCount;
            max = Mathf.Max(minCount, maxCount);
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

    // 1개 이상 떨어질 확률
    public float DropChance()
    {
        if (!HasWeights) return chance;

        float total = TotalWeight();
        if (total <= 0f) return 0f;

        float zero = 0f;
        foreach (var w in countWeights)
        {
            if (w.count == 0 && w.weight > 0f) zero += w.weight;
        }
        return 1f - zero / total;
    }

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
