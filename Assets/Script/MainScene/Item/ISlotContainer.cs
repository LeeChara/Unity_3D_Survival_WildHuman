using System;
using UnityEngine;

// 아이템 칸 묶음 (플레이어 인벤토리, 재련기 등)
// 칸 사이 이동은 SlotTransfer가 맡고, 각 묶음은 칸 내용과 놓을 수 있는지만 알려 줌
public interface ISlotContainer
{
    int SlotCount { get; }
    ItemStack Get(int index);

    // 내용이 바뀐 칸 번호
    event Action<int> SlotChanged;

    // 이 칸에 해당 아이템을 놓을 수 있는지 (예: 재련기 결과 칸은 꺼내기만 가능)
    bool CanPlace(int index, ItemData item);

    // 칸 내용을 직접 바꾼 뒤 호출해 SlotChanged를 알림
    void NotifyChanged(int index);
}

// 같은 묶음 안 또는 서로 다른 묶음 사이의 칸 이동
public static class SlotTransfer
{
    // src 칸에서 amount개를 dst 칸으로 옮김
    // 빈칸이면 놓고, 같은 아이템이면 최대 개수까지 합치고(남은 건 원래 칸에 유지),
    // 다른 아이템이면 전부 옮길 때만 서로 바꿈 (일부만 옮기려 하거나 바꾼 아이템을 원래 칸에 놓을 수 없으면 취소)
    public static void Move(ISlotContainer src, int from, ISlotContainer dst, int to, int amount)
    {
        if (src == dst && from == to) return;

        ItemStack a = src.Get(from);
        ItemStack b = dst.Get(to);
        if (a.IsEmpty || !dst.CanPlace(to, a.item)) return;

        amount = Mathf.Clamp(amount, 1, a.count);

        if (b.IsEmpty)
        {
            b.item = a.item;
            b.count = amount;
            a.count -= amount;
        }
        else if (b.item == a.item)
        {
            int put = Mathf.Min(amount, b.item.maxStack - b.count);
            if (put <= 0) return;
            b.count += put;
            a.count -= put;
        }
        else if (amount == a.count && src.CanPlace(from, b.item))
        {
            (a.item, b.item) = (b.item, a.item);
            (a.count, b.count) = (b.count, a.count);
        }
        else
        {
            return;
        }

        if (a.count <= 0) a.Clear();

        src.NotifyChanged(from);
        dst.NotifyChanged(to);
    }

    // src 칸의 묶음을 dst의 [start, end) 칸으로 최대한 옮김 (Shift+클릭)
    // 같은 아이템이 덜 찬 칸을 먼저 채우고, 남으면 앞쪽 빈칸부터 채움. 옮긴 개수를 반환
    public static int QuickMove(ISlotContainer src, int from, ISlotContainer dst, int start, int end)
    {
        ItemStack stack = src.Get(from);
        if (stack.IsEmpty) return 0;

        ItemData item = stack.item;
        int moved = 0;

        for (int pass = 0; pass < 2 && stack.count > 0; pass++)
        {
            for (int i = start; i < end && stack.count > 0; i++)
            {
                if (dst == src && i == from) continue;

                ItemStack slot = dst.Get(i);
                bool fill = pass == 0 ? !slot.IsEmpty && slot.item == item : slot.IsEmpty;
                if (!fill || !dst.CanPlace(i, item)) continue;

                int put = Mathf.Min(stack.count, item.maxStack - (pass == 0 ? slot.count : 0));
                if (put <= 0) continue;

                if (pass == 0) slot.count += put;
                else
                {
                    slot.item = item;
                    slot.count = put;
                }
                stack.count -= put;
                moved += put;
                dst.NotifyChanged(i);
            }
        }

        if (moved > 0)
        {
            if (stack.count <= 0) stack.Clear();
            src.NotifyChanged(from);
        }
        return moved;
    }

    // 실제로 뺀 개수를 반환
    public static int Remove(ISlotContainer container, int index, int count)
    {
        ItemStack slot = container.Get(index);
        if (slot.IsEmpty || count <= 0) return 0;

        int removed = Mathf.Min(count, slot.count);
        slot.count -= removed;
        if (slot.count <= 0) slot.Clear();

        container.NotifyChanged(index);
        return removed;
    }
}
