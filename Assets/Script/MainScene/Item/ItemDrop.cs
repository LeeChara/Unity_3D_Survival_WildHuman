using System;
using UnityEngine;

// 드랍 테이블의 한 항목
[Serializable]
public class ItemDrop
{
    public ItemData item;
    [Min(0)] public int minCount = 1;
    [Min(0)] public int maxCount = 1;
    [Tooltip("드랍 확률 (1이면 항상 드랍)")]
    [Range(0f, 1f)] public float chance = 1f;
}
