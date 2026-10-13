using System.Collections.Generic;
using UnityEngine;

// 월드 구조물 종류 목록 (생성 순서 = 목록 순서, 세이브의 id로 종류를 찾을 때도 사용)
[CreateAssetMenu(fileName = "WorldStructureDatabase", menuName = "World/WorldStructureDatabase")]
public class WorldStructureDatabase : ScriptableObject
{
    public List<WorldStructureData> structures = new();

    public WorldStructureData Get(string id)
    {
        foreach (var data in structures)
        {
            if (data != null && data.id == id) return data;
        }
        return null;
    }
}
