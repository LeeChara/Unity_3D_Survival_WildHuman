using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

// 슬롯 기반 인벤토리 데이터 (UI와 분리, UI는 SlotChanged 이벤트만 보고 갱신)
// 슬롯 순서는 화면의 좌상단부터: 0~9 핫바, 10~39 가방 3줄
public class Inventory : MonoBehaviour, IItemReceiver, ISaveable
{
    public const int SlotsPerRow = 10;

    [SerializeField, Min(1)] private int slotCount = 40;
    [Tooltip("시작할 때 들고 있는 아이템 (앞쪽 칸부터 채움)")]
    [SerializeField] private ItemStack[] startingItems;

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

    // UI 등이 SlotChanged를 구독한 뒤에 채우도록 Start에서 추가
    private void Start()
    {
        if (startingItems == null) return;

        foreach (ItemStack stack in startingItems)
        {
            if (!stack.IsEmpty) TryAdd(stack.item, stack.count);
        }
    }

    public ItemStack Get(int index) => slots[index];

    #region 저장

    public string SaveKey => "inventory";

    // 칸 순서 그대로 저장 (빈칸은 null)
    public JToken Save()
    {
        var array = new JArray();
        foreach (ItemStack slot in slots)
        {
            array.Add(slot.IsEmpty ? (JToken)JValue.CreateNull() : new JObject { ["id"] = slot.item.id, ["count"] = slot.count });
        }
        return new JObject { ["slots"] = array };
    }

    // 모든 칸을 교체하므로 Start에서 받은 시작 아이템은 남지 않음
    // 지금은 없는 아이템(삭제된 에셋 등)의 칸은 비워 둠
    public void Load(JToken data)
    {
        var array = data["slots"] as JArray;

        for (int i = 0; i < slotCount; i++)
        {
            ItemStack slot = slots[i];
            slot.Clear();

            JToken saved = array != null && i < array.Count ? array[i] : null;
            if (saved is JObject stack)
            {
                ItemData item = SaveRegistry.Active.GetItem(stack.Value<string>("id"));
                if (item != null)
                {
                    slot.item = item;
                    slot.count = Mathf.Clamp(stack.Value<int>("count"), 1, item.maxStack);
                }
            }
            SlotChanged?.Invoke(i);
        }
    }

    #endregion

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

    // 모든 칸에 있는 해당 아이템의 총 개수
    public int CountOf(ItemData item)
    {
        if (item == null) return 0;

        int total = 0;
        foreach (ItemStack slot in slots)
        {
            if (!slot.IsEmpty && slot.item == item) total += slot.count;
        }
        return total;
    }

    // 여러 칸에 걸쳐 해당 아이템을 count개 뺌 (실제로 뺀 개수를 반환)
    // 손에 든 핫바 칸이 마지막에 줄어들도록 가방 뒤쪽 칸부터 뺌
    public int RemoveItem(ItemData item, int count)
    {
        if (item == null || count <= 0) return 0;

        int remaining = count;
        for (int i = slotCount - 1; i >= 0 && remaining > 0; i--)
        {
            ItemStack slot = slots[i];
            if (slot.IsEmpty || slot.item != item) continue;

            remaining -= Remove(i, remaining);
        }
        return count - remaining;
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
