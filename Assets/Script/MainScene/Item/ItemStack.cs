using System;

// 인벤토리 한 칸의 내용 (아이템 종류 + 개수)
[Serializable]
public class ItemStack
{
    public ItemData item;
    public int count;

    public bool IsEmpty => item == null || count <= 0;

    public void Clear()
    {
        item = null;
        count = 0;
    }
}
