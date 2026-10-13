using Newtonsoft.Json.Linq;
using UnityEngine;

// 월드 구조물 안의 상자를 처음 생길 때 한 번만 드롭 테이블로 채움 (예: 울림 유적의 상자)
// 채웠다는 기록을 상자 상태와 함께 저장하므로 비우고 떠났다 돌아와도 다시 채워지지 않음
[RequireComponent(typeof(StorageChest))]
public class LootFill : MonoBehaviour, ISaveable
{
    // 모든 항목이 확률에 실패해 빈 상자가 되면 다시 굴리는 최대 횟수
    private const int MaxRolls = 10;

    [SerializeField] private DropTable lootTable;

    private bool filled;

    public string SaveKey => "loot";

    // 저장된 상태는 생성 직후(Start 전)에 불러와지므로 처음 생긴 상자만 채움
    private void Start()
    {
        if (filled || lootTable == null) return;
        filled = true;

        var chest = GetComponent<StorageChest>();
        for (int roll = 0; roll < MaxRolls; roll++)
        {
            if (Fill(chest)) return;
        }
    }

    // 하나라도 넣었으면 true
    private bool Fill(StorageChest chest)
    {
        bool any = false;
        foreach (ItemDrop drop in lootTable.Drops)
        {
            if (drop.item == null) continue;

            int count = drop.Roll();
            while (count > 0)
            {
                int index = FindEmpty(chest);
                if (index < 0) return any;

                ItemStack slot = chest.Get(index);
                int amount = Mathf.Min(count, drop.item.maxStack);
                slot.item = drop.item;
                slot.count = amount;
                chest.NotifyChanged(index);
                count -= amount;
                any = true;
            }
        }
        return any;
    }

    private static int FindEmpty(StorageChest chest)
    {
        for (int i = 0; i < chest.SlotCount; i++)
        {
            if (chest.Get(i).IsEmpty) return i;
        }
        return -1;
    }

    public JToken Save() => filled ? new JObject { ["filled"] = true } : null;

    public void Load(JToken data)
    {
        filled = data.Value<bool>("filled");
    }
}
