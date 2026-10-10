using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

// 모닥불에 아이템 1개를 올려 두면 시간이 지나 결과물로 바뀜 (예: 날고기 → 구운 고기)
// 우클릭: 비어 있으면 들고 있는 재료를 올리고, 굽는 중이면 재료를, 다 구워졌으면 결과물을 회수
// 인벤토리가 가득 차 회수하지 못한 만큼은 모닥불 앞에 떨어뜨림
public class CampfireCooker : MonoBehaviour, IInteractable, ISaveable
{
    [SerializeField] private CookingBook cookingBook;
    [Tooltip("이 거리(수평) 안에 있으면 상호작용 가능")]
    [SerializeField, Min(0f)] private float range = 3f;

    [Header("표시")]
    [Tooltip("모닥불 위에 올린 아이템을 그릴 스프라이트")]
    [SerializeField] private SpriteRenderer itemRenderer;
    [Tooltip("굽는 중에만 켜지는 원형 타이머 (배경 포함)")]
    [SerializeField] private GameObject timerRoot;
    [Tooltip("진행도만큼 채워지는 이미지 (Filled, Radial 360)")]
    [SerializeField] private Image timerFill;

    // 올려 둔 재료의 레시피 (비어 있으면 null)
    private CookingRecipe recipe;
    private float remaining;

    private bool IsDone => recipe != null && remaining <= 0f;

    private void Awake()
    {
        if (TryGetComponent(out Health health)) health.Died += OnDied;
        Refresh();
    }

    private void Update()
    {
        if (recipe == null || IsDone) return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            remaining = 0f;
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
        if (recipe != null)
        {
            Give(inventory, IsDone ? recipe.result : recipe.input);
            Clear();
            return;
        }

        ItemStack stack = hotbar.SelectedStack;
        if (stack.IsEmpty) return;

        CookingRecipe found = cookingBook.Find(stack.item);
        if (found == null) return;

        inventory.Remove(hotbar.SelectedIndex, 1);
        recipe = found;
        remaining = found.seconds;
        Refresh();
    }

    private void Give(Inventory inventory, ItemData item)
    {
        if (inventory.TryAdd(item, 1) > 0) return;
        if (ItemDropper.Instance != null) ItemDropper.Instance.Drop(item, 1, transform.position);
    }

    // 모닥불이 부서지면 올려 둔 재료(또는 결과물)를 떨어뜨림
    private void OnDied()
    {
        if (recipe == null) return;

        if (ItemDropper.Instance != null) ItemDropper.Instance.Drop(IsDone ? recipe.result : recipe.input, 1, transform.position);
        Clear();
    }

    private void Clear()
    {
        recipe = null;
        remaining = 0f;
        Refresh();
    }

    private void Refresh()
    {
        itemRenderer.sprite = recipe == null ? null : (IsDone ? recipe.result.icon : recipe.input.icon);
        timerRoot.SetActive(recipe != null && !IsDone);
        if (recipe != null) timerFill.fillAmount = 1f - remaining / recipe.seconds;
    }

    #region 저장

    public string SaveKey => "cooker";

    // 비어 있으면 저장하지 않음
    public JToken Save()
    {
        if (recipe == null) return null;
        return new JObject { ["input"] = recipe.input.id, ["remaining"] = remaining };
    }

    // 지금은 없는 재료이거나 레시피가 사라졌으면 비워 둠
    public void Load(JToken data)
    {
        ItemData input = SaveRegistry.Active.GetItem(data.Value<string>("input"));
        recipe = cookingBook.Find(input);
        remaining = recipe != null ? Mathf.Clamp(data.Value<float>("remaining"), 0f, recipe.seconds) : 0f;
        Refresh();
    }

    #endregion
}
