using System.Collections.Generic;
using UnityEngine;

// 드랍 항목 묶음. 자원 Prop과 몬스터가 공유해서 참조
[CreateAssetMenu(fileName = "DropTable", menuName = "Item/DropTable")]
public class DropTable : ScriptableObject
{
    [SerializeField] private ItemDrop[] drops;

    public IReadOnlyList<ItemDrop> Drops => drops;
}
