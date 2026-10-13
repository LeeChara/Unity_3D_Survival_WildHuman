using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

// 아이템을 보관하는 상자. 우클릭하면 ChestUI 창이 인벤토리와 함께 열림
// 칸에는 아무 아이템이나 놓을 수 있고, 부서지면 안의 아이템을 모두 떨어뜨림 (불타 부서지면 일부가 사라지고 남은 것은 구운 형태로)
public class StorageChest : MonoBehaviour, IInteractable, ISaveable, ISlotContainer
{
    [SerializeField, Min(1)] private int slotCount = 20;
    [Tooltip("이 거리(수평) 안에 있으면 상호작용 가능 (창도 이 거리를 벗어나면 닫힘)")]
    [SerializeField, Min(0f)] private float range = 3f;
    [Tooltip("창이 열려 있는 동안 쓸 스프라이트 (닫힌 스프라이트는 spriteRenderer에 처음 들어 있던 것)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite openSprite;

    public int SlotCount => slotCount;

    public event Action<int> SlotChanged;

    private ItemStack[] slots;
    private Combustible combustible;
    private Sprite closedSprite;

    private void Awake()
    {
        slots = new ItemStack[slotCount];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new ItemStack();
        }

        if (TryGetComponent(out Health health)) health.Died += OnDied;
        combustible = GetComponent<Combustible>();

        if (spriteRenderer != null) closedSprite = spriteRenderer.sprite;
    }

    // ChestUI가 창을 열고 닫을 때 호출
    public void SetOpen(bool open)
    {
        if (spriteRenderer == null || openSprite == null) return;
        spriteRenderer.sprite = open ? openSprite : closedSprite;
    }

    public ItemStack Get(int index) => slots[index];

    public bool CanPlace(int index, ItemData item) => true;

    public void NotifyChanged(int index)
    {
        SlotChanged?.Invoke(index);
    }

    public bool CanInteract(Vector3 from)
    {
        Vector3 offset = from - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= range * range;
    }

    public void Interact(Inventory inventory, PlayerHotbar hotbar)
    {
        if (ChestUI.Instance != null) ChestUI.Instance.Open(this, inventory.transform);
    }

    public string GetInteractPreview(PlayerHotbar hotbar) => "열기";

    // 설치물이 부서지면 칸에 있던 아이템을 모두 떨어뜨림
    private void OnDied()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack slot = slots[i];
            if (slot.IsEmpty) continue;

            var (item, count) = combustible != null ? combustible.BurnStored(slot.item, slot.count) : (slot.item, slot.count);
            if (count > 0 && ItemDropper.Instance != null) ItemDropper.Instance.Drop(item, count, transform.position);
            slot.Clear();
            NotifyChanged(i);
        }
    }

    #region 저장

    public string SaveKey => "chest";

    // 칸이 모두 비어 있으면 저장하지 않음
    public JToken Save()
    {
        var array = new JArray();
        bool any = false;
        foreach (ItemStack slot in slots)
        {
            any |= !slot.IsEmpty;
            array.Add(slot.IsEmpty ? (JToken)JValue.CreateNull() : new JObject { ["id"] = slot.item.id, ["count"] = slot.count });
        }
        if (!any) return null;
        return new JObject { ["slots"] = array };
    }

    // 지금은 없는 아이템의 칸은 비워 둠. 칸 수가 바뀌었으면 앞쪽 칸부터 채움
    public void Load(JToken data)
    {
        var array = data["slots"] as JArray;
        for (int i = 0; i < slots.Length; i++)
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
}
