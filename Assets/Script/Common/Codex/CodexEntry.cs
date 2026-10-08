using UnityEngine;

// 도감 목록의 한 칸 (원본 데이터는 그대로 참조만 함)
// data: 아이템·설치물 = ItemData, 자원 = PropData, 몬스터 = MonsterData, 지형 = BiomeData
public class CodexEntry
{
    public readonly CodexCategory category;
    public readonly string name;
    public readonly Sprite icon;
    public readonly Object data;
    // 월드에 놓이는 대상(자원·설치물·몬스터)의 프리팹 (아이템·지형은 null)
    public readonly GameObject prefab;

    public CodexEntry(CodexCategory category, string name, Sprite icon, Object data, GameObject prefab)
    {
        this.category = category;
        this.name = string.IsNullOrEmpty(name) ? data.name : name;
        this.icon = icon;
        this.data = data;
        this.prefab = prefab;
    }
}
