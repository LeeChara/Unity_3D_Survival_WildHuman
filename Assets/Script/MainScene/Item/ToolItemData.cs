using UnityEngine;

// 핫바에서 고른 채 공격하면 공격에 도구 종류와 피해량이 적용되는 아이템 (예: 망치 → 설치물에 큰 피해)
// 무기(창 등)는 toolType을 None으로 두고 피해량만 사용
[CreateAssetMenu(fileName = "ToolItemData", menuName = "Item/ToolItemData")]
public class ToolItemData : ItemData
{
    public ToolType toolType;

    [Tooltip("이 도구로 공격할 때의 기본 피해량 (도구 배율은 맞는 대상에서 따로 적용)")]
    [Min(0)] public int damage = 10;

    [Tooltip("손에 들었을 때 표시할 스프라이트 (비워두면 아무것도 표시하지 않음)")]
    public Sprite heldSprite;

    public override string GetStatText()
    {
        return $"{damage} 피해";
    }
}
