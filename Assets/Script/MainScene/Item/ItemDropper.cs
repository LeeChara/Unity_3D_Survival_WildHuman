using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// 월드 아이템 생성·반환 관리
// 모든 아이템이 같은 프리팹(WorldItem)을 쓰고 ItemData로 외형만 바꾸므로 풀은 하나
public class ItemDropper : MonoBehaviour
{
    [SerializeField] private WorldItem worldItemPrefab;
    [SerializeField] private GameSetting gameSetting;

    public static ItemDropper Instance { get; private set; }

    private ObjectPool<WorldItem> pool;

    private void Awake()
    {
        Instance = this;

        pool = new ObjectPool<WorldItem>(
            createFunc: () => Instantiate(worldItemPrefab, transform),
            actionOnGet: item => item.gameObject.SetActive(true),
            actionOnRelease: item => item.gameObject.SetActive(false),
            actionOnDestroy: item => Destroy(item.gameObject));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Drop(IReadOnlyList<ItemDrop> drops, Vector3 origin)
    {
        foreach (var drop in drops)
        {
            if (drop.item == null) continue;
            if (Random.value > drop.chance) continue;

            int maxCount = Mathf.Max(drop.minCount, drop.maxCount);
            Drop(drop.item, Random.Range(drop.minCount, maxCount + 1), origin);
        }
    }

    // 정해진 개수를 origin 주변에 흩어 떨어뜨림 (최대 개수를 넘으면 여러 덩어리로 나눔)
    public void Drop(ItemData data, int count, Vector3 origin)
    {
        while (count > 0)
        {
            int stack = Mathf.Min(count, data.maxStack);
            Spawn(data, stack, origin);
            count -= stack;
        }
    }

    // 인벤토리에서 던진 아이템: 흩어지지 않고 방향으로 정해진 거리만큼 날아가 착지
    public void Throw(ItemData data, int count, Vector3 origin, Vector3 direction)
    {
        ItemRule rule = gameSetting.item;
        Vector3 landing = origin + direction.normalized * rule.throwDistance;

        while (count > 0)
        {
            int stack = Mathf.Min(count, data.maxStack);
            pool.Get().Init(data, stack, origin, landing, rule, rule.throwPickupDelay);
            count -= stack;
        }
    }

    public void Release(WorldItem item)
    {
        pool.Release(item);
    }

    private void Spawn(ItemData data, int count, Vector3 origin)
    {
        ItemRule rule = gameSetting.item;

        Vector2 offset = Random.insideUnitCircle * rule.scatterRadius;
        Vector3 landing = origin + new Vector3(offset.x, 0f, offset.y);

        pool.Get().Init(data, count, origin, landing, rule);
    }
}
