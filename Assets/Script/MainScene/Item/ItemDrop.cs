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

    // 확률과 개수 범위로 이번에 떨어뜨릴 개수를 정함 (확률에 실패하면 0)
    public int Roll()
    {
        if (Random.value > chance) return 0;
        return Random.Range(minCount, Mathf.Max(minCount, maxCount) + 1);
    }
}
