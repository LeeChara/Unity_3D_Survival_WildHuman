using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

// 재료 칸에 넣은 재료가 레시피를 만족하고 결과 칸에 자리가 있으면 시간이 흐르고,
// 다 되면 그때 재료를 빼고 결과물을 결과 칸에 쌓음 (재료가 남아 있으면 이어서 재련)
// 진행 중에 재료를 빼서 레시피가 바뀌거나 깨지면 진행도는 처음부터
// 우클릭하면 SmelterUI 창이 인벤토리와 함께 열림. 창을 닫아도 재련은 계속됨
public class Smelter : MonoBehaviour, IInteractable, ISaveable, ISlotContainer
{
    [SerializeField] private SmeltingBook smeltingBook;
    [SerializeField, Min(1)] private int inputSlots = 3;
    [SerializeField, Min(1)] private int outputSlots = 2;
    [Tooltip("이 거리(수평) 안에 있으면 상호작용 가능 (창도 이 거리를 벗어나면 닫힘)")]
    [SerializeField, Min(0f)] private float range = 3f;

    [Header("표시")]
    [Tooltip("재련 중일 때만 켜지는 원형 타이머 (배경 포함)")]
    [SerializeField] private GameObject timerRoot;
    [Tooltip("진행도만큼 채워지는 이미지 (Filled, Radial 360)")]
    [SerializeField] private Image timerFill;
    [Tooltip("재련하는 동안만 켜지는 굴뚝 연기 (비워두면 없음)")]
    [SerializeField] private LoopingEffect workEffect;

    public SmeltingBook Book => smeltingBook;
    public int InputSlots => inputSlots;
    public int OutputSlots => outputSlots;
    public int SlotCount => inputSlots + outputSlots;

    // 지금 진행 중인 레시피 (없으면 null)와 진행도 (0~1)
    public SmeltingRecipe Current { get; private set; }
    public float Progress => Current != null ? Mathf.Clamp01(elapsed / Current.seconds) : 0f;

    public event Action<int> SlotChanged;

    // 앞쪽 inputSlots개는 재료 칸, 나머지는 결과 칸
    private ItemStack[] slots;
    private float elapsed;
    // 칸이 바뀌어 레시피를 다시 찾아야 함
    private bool dirty = true;

    private void Awake()
    {
        slots = new ItemStack[SlotCount];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new ItemStack();
        }

        if (TryGetComponent(out Health health)) health.Died += OnDied;
        RefreshTimer();
    }

    private void Update()
    {
        if (dirty) FindRecipe();
        UpdateWorkSound();
        if (workEffect != null && workEffect.enabled != (Current != null)) workEffect.enabled = Current != null;
        if (Current == null) return;

        elapsed += Time.deltaTime;
        if (elapsed >= Current.seconds) Complete();
        RefreshTimer();
    }

    public ItemStack Get(int index) => slots[index];

    public bool IsInput(int index) => index < inputSlots;

    // 재료 칸에는 재련 재료만, 결과 칸에는 아무것도 놓을 수 없음 (꺼내기만 가능)
    public bool CanPlace(int index, ItemData item)
    {
        return IsInput(index) && smeltingBook != null && smeltingBook.IsIngredient(item);
    }

    public void NotifyChanged(int index)
    {
        dirty = true;
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
        if (SmelterUI.Instance != null) SmelterUI.Instance.Open(this, inventory.transform);
    }

    public string GetInteractPreview(PlayerHotbar hotbar) => "열기";

    // 위에서부터 지금 칸으로 할 수 있는 첫 레시피. 레시피가 바뀌면 진행도는 처음부터
    private void FindRecipe()
    {
        dirty = false;

        SmeltingRecipe found = null;
        if (smeltingBook != null && smeltingBook.Recipes != null)
        {
            foreach (SmeltingRecipe recipe in smeltingBook.Recipes)
            {
                if (HasIngredients(recipe) && CanFitResults(recipe))
                {
                    found = recipe;
                    break;
                }
            }
        }

        if (found != Current) elapsed = 0f;
        Current = found;
        RefreshTimer();
    }

    private void Complete()
    {
        SmeltingRecipe recipe = Current;
        elapsed = 0f;

        foreach (Ingredient ingredient in recipe.ingredients)
        {
            RemoveInput(ingredient.item, ingredient.count);
        }
        foreach (Ingredient result in recipe.results)
        {
            AddOutput(result.item, result.count);
        }

        // 다음 프레임에 남은 재료로 다시 레시피를 찾음
        dirty = true;
        Sfx.Play(SfxSetting.Instance?.smeltDone, transform.position);
    }

    // 재련하는 동안만 가동 소리
    private SfxLoop workLoop;

    private void UpdateWorkSound()
    {
        bool working = Current != null;
        if (workLoop == null)
        {
            if (!working || SfxSetting.Instance == null || SfxSetting.Instance.smeltLoop == null) return;
            workLoop = SfxLoop.Ensure(gameObject, SfxSetting.Instance.smeltLoop);
        }
        if (working == workLoop.IsPlaying) return;
        if (working) workLoop.Play();
        else workLoop.Stop();
    }

    private bool HasIngredients(SmeltingRecipe recipe)
    {
        if (recipe.ingredients == null || recipe.ingredients.Length == 0 || recipe.results == null) return false;

        foreach (Ingredient ingredient in recipe.ingredients)
        {
            if (ingredient.item == null) return false;

            // 같은 재료가 여러 줄이면 합쳐서 확인
            int need = 0;
            foreach (Ingredient other in recipe.ingredients)
            {
                if (other.item == ingredient.item) need += other.count;
            }
            if (CountInputs(ingredient.item) < need) return false;
        }
        return true;
    }

    // 결과 칸을 복사해 결과물을 모두 넣어 볼 수 있는지 확인
    private bool CanFitResults(SmeltingRecipe recipe)
    {
        var items = new ItemData[outputSlots];
        var counts = new int[outputSlots];
        for (int i = 0; i < outputSlots; i++)
        {
            ItemStack slot = slots[inputSlots + i];
            items[i] = slot.IsEmpty ? null : slot.item;
            counts[i] = slot.IsEmpty ? 0 : slot.count;
        }

        foreach (Ingredient result in recipe.results)
        {
            if (result.item == null) return false;

            int left = result.count;
            for (int i = 0; i < outputSlots && left > 0; i++)
            {
                if (items[i] != result.item) continue;
                int put = Mathf.Min(left, result.item.maxStack - counts[i]);
                counts[i] += put;
                left -= put;
            }
            for (int i = 0; i < outputSlots && left > 0; i++)
            {
                if (items[i] != null) continue;
                int put = Mathf.Min(left, result.item.maxStack);
                items[i] = result.item;
                counts[i] = put;
                left -= put;
            }
            if (left > 0) return false;
        }
        return true;
    }

    private int CountInputs(ItemData item)
    {
        int total = 0;
        for (int i = 0; i < inputSlots; i++)
        {
            if (!slots[i].IsEmpty && slots[i].item == item) total += slots[i].count;
        }
        return total;
    }

    // 뒤쪽 재료 칸부터 뺌
    private void RemoveInput(ItemData item, int count)
    {
        for (int i = inputSlots - 1; i >= 0 && count > 0; i--)
        {
            ItemStack slot = slots[i];
            if (slot.IsEmpty || slot.item != item) continue;

            int removed = Mathf.Min(count, slot.count);
            slot.count -= removed;
            count -= removed;
            if (slot.count <= 0) slot.Clear();
            NotifyChanged(i);
        }
    }

    // 같은 아이템이 덜 찬 결과 칸을 먼저 채우고, 남으면 앞쪽 빈칸부터 (CanFitResults로 확인한 뒤 호출)
    private void AddOutput(ItemData item, int count)
    {
        for (int pass = 0; pass < 2 && count > 0; pass++)
        {
            for (int i = inputSlots; i < slots.Length && count > 0; i++)
            {
                ItemStack slot = slots[i];
                if (pass == 0 ? slot.IsEmpty || slot.item != item : !slot.IsEmpty) continue;

                int put = Mathf.Min(count, item.maxStack - (pass == 0 ? slot.count : 0));
                if (put <= 0) continue;

                if (pass == 0) slot.count += put;
                else
                {
                    slot.item = item;
                    slot.count = put;
                }
                count -= put;
                NotifyChanged(i);
            }
        }
    }

    // 설치물이 부서지면 칸에 있던 아이템을 모두 떨어뜨림
    private void OnDied()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack slot = slots[i];
            if (slot.IsEmpty) continue;

            if (ItemDropper.Instance != null) ItemDropper.Instance.Drop(slot.item, slot.count, transform.position);
            slot.Clear();
            NotifyChanged(i);
        }
        Current = null;
        elapsed = 0f;
    }

    private void RefreshTimer()
    {
        timerRoot.SetActive(Current != null);
        if (Current != null) timerFill.fillAmount = Progress;
    }

    #region 저장

    public string SaveKey => "smelter";

    // 칸이 모두 비어 있으면 저장하지 않음
    // 진행 중인 레시피는 칸 내용으로 다시 찾을 수 있으므로 진행 시간만 저장
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
        return new JObject { ["slots"] = array, ["elapsed"] = elapsed };
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

        FindRecipe();
        if (Current != null) elapsed = Mathf.Clamp(data.Value<float?>("elapsed") ?? 0f, 0f, Current.seconds);
        RefreshTimer();
    }

    #endregion
}
