using UnityEngine;

// 양동이 (빈 양동이 / 물 양동이 두 에셋이 서로를 짝으로 가짐, 한 칸에 1개)
// - 빈 양동이: 휘두른 범위에 물 칸이 닿으면 물 양동이로 바뀜
// - 물 양동이: 물 속성 장비. 휘두르면 넓은 범위에 피해 없는 물 속성 공격 → 맞은 대상이 젖음
//   한 번 휘두르면 빈 양동이로 바뀜
// 바꾸는 처리는 PlayerBucketSwing이 담당
[CreateAssetMenu(fileName = "BucketItemData", menuName = "Item/BucketItemData")]
public class BucketItemData : ToolItemData
{
    [Header("양동이")]
    [Tooltip("물이 담겨 있는지")]
    public bool filled;
    [Tooltip("물을 뜨거나 쏟으면 바뀌는 짝 양동이")]
    public BucketItemData counterpart;

    public override string GetStatText()
    {
        if (filled) return AppendElement("넓은 범위 · 피해 없음\n한 번 휘두르면 비워짐");
        return $"{damage} 피해\n물가를 향해 휘두르면 물을 뜸";
    }
}
