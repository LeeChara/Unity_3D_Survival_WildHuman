using System;
using UnityEngine;

// 슬롯 기반 인벤토리 데이터 (UI와 분리, UI는 SlotChanged 이벤트만 보고 갱신)
// 슬롯 순서는 화면의 좌상단부터: 0~9 핫바, 10~39 가방 3줄
public class Inventory : MonoBehaviour, IItemReceiver
{
    public const int SlotsPerRow = 10;

    [SerializeField, Min(1)] private int slotCount = 40;

    private ItemStack[] slots;

    public int SlotCount => slotCount;

    // 내용이 바뀐 슬롯 번호
    public event Action<int> SlotChanged;

    private void Awake()
    {
        slots = new ItemStack[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            slots[i] = new ItemStack();
        }
    }

    public ItemStack Get(int index) => slots[index];

    // 같은 아이템이 덜 찬 칸을 먼저 채우고, 남으면 앞쪽 빈칸부터 채움
    public int TryAdd(ItemData item, int count)
    {
        if (item == null || count <= 0) return 0;

        int remaining = count;

        for (int i = 0; i < slotCount && remaining > 0; i++)
        {
            ItemStack slot = slots[i];
            if (slot.IsEmpty || slot.item != item || slot.count >= item.maxStack) continue;

            int put = Mathf.Min(remaining, item.maxStack - slot.count);
            slot.count += put;
            remaining -= put;
            SlotChanged?.Invoke(i);
        }

        for (int i = 0; i < slotCount && remaining > 0; i++)
        {
            ItemStack slot = slots[i];
            if (!slot.IsEmpty) continue;

            int put = Mathf.Min(remaining, item.maxStack);
            slot.item = item;
            slot.count = put;
            remaining -= put;
            SlotChanged?.Invoke(i);
        }

        // 가득 차서 못 받은 만큼은 월드 아이템에 그대로 남음
        return count - remaining;
    }

    // from 칸에서 amount개를 to 칸으로 옮김
    // 빈칸이면 놓고, 같은 아이템이면 최대 개수까지 합치고(남은 건 원래 칸에 유지),
    // 다른 아이템이면 전부 옮길 때만 서로 바꿈 (일부만 옮기려 하면 취소)
    public void Move(int from, int to, int amount)
    {
        if (from == to) return;

        ItemStack src = slots[from];
        ItemStack dst = slots[to];
        if (src.IsEmpty) return;

        amount = Mathf.Clamp(amount, 1, src.count);

        if (dst.IsEmpty)
        {
            dst.item = src.item;
            dst.count = amount;
            src.count -= amount;
        }
        else if (dst.item == src.item)
        {
            int put = Mathf.Min(amount, dst.item.maxStack - dst.count);
            if (put <= 0) return;
            dst.count += put;
            src.count -= put;
        }
        else if (amount == src.count)
        {
            slots[from] = dst;
            slots[to] = src;
        }
        else
        {
            return;
        }

        if (slots[from].count <= 0) slots[from].Clear();

        SlotChanged?.Invoke(from);
        SlotChanged?.Invoke(to);
    }

    // 실제로 뺀 개수를 반환
    public int Remove(int index, int count)
    {
        ItemStack slot = slots[index];
        if (slot.IsEmpty || count <= 0) return 0;

        int removed = Mathf.Min(count, slot.count);
        slot.count -= removed;
        if (slot.count <= 0) slot.Clear();

        SlotChanged?.Invoke(index);
        return removed;
    }
}
