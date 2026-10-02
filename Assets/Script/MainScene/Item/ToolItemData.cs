using UnityEngine;

// 핫바에서 고른 채 공격하면 공격에 도구 종류가 적용되는 아이템 (예: 망치 → 설치물에 큰 피해)
[CreateAssetMenu(fileName = "ToolItemData", menuName = "Item/ToolItemData")]
public class ToolItemData : ItemData
{
    public ToolType toolType;
}
