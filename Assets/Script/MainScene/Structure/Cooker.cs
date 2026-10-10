using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

// 설치물에 같은 재료를 capacity개까지 올려 두면 한 번에 하나씩 차례로 결과물로 바뀜
// (예: 모닥불 날고기 → 구운 고기 1개, 재련기 철광석 → 철 주괴 3개)
// 우클릭: 완성품이 있으면 모두 회수 → 없으면 들고 있는 재료를 1개 올림 → 그것도 안 되면 대기 중인 재료를 모두 꺼냄
// 인벤토리가 가득 차 회수하지 못한 만큼은 설치물 앞에 떨어뜨림
public class Cooker : MonoBehaviour, IInteractable, ISaveable
{
    [SerializeField] private CookingBook cookingBook;
    [Tooltip("한 번에 올려 둘 수 있는 개수 (대기 + 완성)")]
    [SerializeField, Min(1)] private int capacity = 1;
    [Tooltip("이 거리(수평) 안에 있으면 상호작용 가능")]
    [SerializeField, Min(0f)] private float range = 3f;

    [Header("표시")]
    [Tooltip("올린 아이템을 그릴 스프라이트 (앞쪽부터 완성품, 그다음 대기 중인 재료 순서, capacity개)")]
    [SerializeField] private SpriteRenderer[] itemRenderers;
    [Tooltip("재료가 있을 때만 켜지는 원형 타이머 (배경 포함)")]
    [SerializeField] private GameObject timerRoot;
    [Tooltip("지금 처리 중인 1개의 진행도만큼 채워지는 이미지 (Filled, Radial 360)")]
    [SerializeField] private Image timerFill;

    public CookingBook Book => cookingBook;
    public int Capacity => capacity;

    // 올려 둔 재료의 레시피 (비어 있으면 null)
    private CookingRecipe recipe;
    // 아직 처리되지 않은 재료 수 (맨 앞 1개가 처리 중)
    private int pending;
    // 처리가 끝나 회수를 기다리는 결과물 수
    private int done;
    // 처리 중인 1개의 남은 시간
    private float remaining;

    private void Awake()
    {
        if (TryGetComponent(out Health health)) health.Died += OnDied;
        Refresh();
    }

    private void Update()
    {
        if (pending <= 0) return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            pending--;
            done++;
            remaining = pending > 0 ? recipe.seconds : 0f;
            Refresh();
            return;
        }
        timerFill.fillAmount = 1f - remaining / recipe.seconds;
    }

    public bool CanInteract(Vector3 from)
    {
        Vector3 offset = from - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= range * range;
    }

    public void Interact(Inventory inventory, PlayerHotbar hotbar)
    {
        if (done > 0)
        {
            Give(inventory, recipe.result, done);
            done = 0;
        }
        else if (!TryAdd(inventory, hotbar) && pending > 0)
        {
            Give(inventory, recipe.input, pending);
            pending = 0;
            remaining = 0f;
        }

        if (pending == 0 && done == 0) recipe = null;
        Refresh();
    }

    // 들고 있는 재료가 지금 올려 둔 것과 같고(또는 비어 있고) 자리가 남으면 1개 올림
    private bool TryAdd(Inventory inventory, PlayerHotbar hotbar)
    {
        ItemStack stack = hotbar.SelectedStack;
        if (stack.IsEmpty || pending + done >= capacity) return false;

        CookingRecipe found = cookingBook.Find(stack.item);
        if (found == null || (recipe != null && recipe != found)) return false;

        inventory.Remove(hotbar.SelectedIndex, 1);
        recipe = found;
        if (pending == 0) remaining = found.seconds;
        pending++;
        return true;
    }

    private void Give(Inventory inventory, ItemData item, int count)
    {
        int left = count - inventory.TryAdd(item, count);
        if (left > 0 && ItemDropper.Instance != null) ItemDropper.Instance.Drop(item, left, transform.position);
    }

    // 설치물이 부서지면 올려 둔 재료와 결과물을 떨어뜨림
    private void OnDied()
    {
        if (recipe == null || ItemDropper.Instance == null) return;

        if (pending > 0) ItemDropper.Instance.Drop(recipe.input, pending, transform.position);
        if (done > 0) ItemDropper.Instance.Drop(recipe.result, done, transform.position);
        recipe = null;
        pending = 0;
        done = 0;
    }

    private void Refresh()
    {
        for (int i = 0; i < itemRenderers.Length; i++)
        {
            Sprite sprite = null;
            if (recipe != null)
            {
                if (i < done) sprite = recipe.result.icon;
                else if (i < done + pending) sprite = recipe.input.icon;
            }
            itemRenderers[i].sprite = sprite;
        }

        timerRoot.SetActive(pending > 0);
        if (pending > 0) timerFill.fillAmount = 1f - remaining / recipe.seconds;
    }

    #region 저장

    public string SaveKey => "cooker";

    // 비어 있으면 저장하지 않음
    public JToken Save()
    {
        if (recipe == null) return null;
        return new JObject { ["input"] = recipe.input.id, ["pending"] = pending, ["done"] = done, ["remaining"] = remaining };
    }

    // 지금은 없는 재료이거나 레시피가 사라졌으면 비워 둠
    // pending이 없는 예전 세이브(1개짜리 모닥불)는 남은 시간으로 완성 여부를 판단
    public void Load(JToken data)
    {
        ItemData input = SaveRegistry.Active.GetItem(data.Value<string>("input"));
        recipe = cookingBook.Find(input);
        pending = 0;
        done = 0;
        remaining = 0f;

        if (recipe != null)
        {
            float savedRemaining = data.Value<float?>("remaining") ?? 0f;
            if (data["pending"] != null)
            {
                pending = Mathf.Max(0, data.Value<int>("pending"));
                done = Mathf.Max(0, data.Value<int>("done"));
            }
            else if (savedRemaining > 0f) pending = 1;
            else done = 1;

            // 용량이 줄었으면 완성품부터 남기고 넘치는 만큼은 버림
            done = Mathf.Min(done, capacity);
            pending = Mathf.Min(pending, capacity - done);
            remaining = pending > 0 ? Mathf.Clamp(savedRemaining, 0.01f, recipe.seconds) : 0f;
            if (pending == 0 && done == 0) recipe = null;
        }
        Refresh();
    }

    #endregion
}
