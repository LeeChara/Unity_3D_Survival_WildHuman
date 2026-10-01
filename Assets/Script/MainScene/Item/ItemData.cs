using UnityEngine;

// 아이템 한 종류의 정의 (아이템마다 에셋 1개)
// 월드에 떨어진 아이템(WorldItem)과 인벤토리 슬롯은 이 에셋을 참조만 함
[CreateAssetMenu(fileName = "ItemData", menuName = "Item/ItemData")]
public class ItemData : ScriptableObject
{
    [Tooltip("저장·로드 시 식별용 고유 ID (한 번 정하면 변경하지 않음)")]
    public string id;
    public string displayName;
    public Sprite icon;
    [Tooltip("한 칸에 쌓을 수 있는 최대 개수")]
    [Min(1)] public int maxStack = 99;
}
