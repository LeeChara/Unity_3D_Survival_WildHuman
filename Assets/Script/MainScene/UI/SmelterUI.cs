using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 재련기를 우클릭하면 인벤토리와 함께 열리는 재련기 창 (재료 칸 → 진행도 → 결과 칸)
// 칸 이동·Shift+클릭은 InventoryUI가 처리하고, 이 창은 칸 연결과 표시만 맡음
// 인벤토리를 닫거나, 재련기에서 멀어지거나, 재련기가 부서지면 닫힘
public class SmelterUI : MonoBehaviour
{
    public static SmelterUI Instance { get; private set; }

    // 이 스크립트는 항상 활성 상태인 부모에 두고, 패널만 켜고 끔
    [SerializeField] private RectTransform panel;
    [SerializeField] private InventoryUI inventoryUI;
    [Tooltip("재료 칸 (재련기의 재료 칸 수보다 많은 칸은 숨김)")]
    [SerializeField] private InventorySlotUI[] inputSlotUIs;
    [Tooltip("결과 칸 (재련기의 결과 칸 수보다 많은 칸은 숨김)")]
    [SerializeField] private InventorySlotUI[] outputSlotUIs;
    [Tooltip("진행도만큼 채워지는 이미지 (Filled, Horizontal)")]
    [SerializeField] private Image progressFill;
    [Tooltip("지금 만드는 결과물 표시")]
    [SerializeField] private TMP_Text recipeText;
    [SerializeField] private string idleText = "알맞은 재료를 넣으세요";

    public bool IsOpen => smelter != null;

    private Smelter smelter;
    private Transform player;
    private SmeltingRecipe shownRecipe;
    private bool hasShownRecipe;

    private void Awake()
    {
        Instance = this;
        panel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        inventoryUI.OpenStateChanged += OnInventoryOpenStateChanged;
        inventoryUI.ExternalChanged += OnExternalChanged;
    }

    private void OnDisable()
    {
        inventoryUI.OpenStateChanged -= OnInventoryOpenStateChanged;
        inventoryUI.ExternalChanged -= OnExternalChanged;
    }

    public void Open(Smelter target, Transform from)
    {
        if (target == null) return;
        if (smelter != target) Close();

        smelter = target;
        player = from;
        smelter.SlotChanged += RefreshSlot;

        BindSlots(inputSlotUIs, smelter.InputSlots, 0);
        BindSlots(outputSlotUIs, smelter.OutputSlots, smelter.InputSlots);

        // 제작 창이 External을 보고 숨을 수 있도록 인벤토리를 열기 전에 연결
        panel.gameObject.SetActive(true);
        inventoryUI.SetExternal(smelter, panel);
        inventoryUI.SetOpen(true);

        hasShownRecipe = false;
        RefreshProgress();
    }

    public void Close()
    {
        if (smelter == null && !panel.gameObject.activeSelf) return;

        // 부서진 재련기도 C# 객체는 남아 있으므로 이벤트 해제는 그대로 가능
        Smelter closing = smelter;
        if ((object)smelter != null) smelter.SlotChanged -= RefreshSlot;
        smelter = null;
        player = null;

        // 다른 창이 칸 묶음을 이어받아 닫히는 경우에는 그 연결을 건드리지 않음
        if (inventoryUI.External == (ISlotContainer)closing) inventoryUI.SetExternal(null, null);
        panel.gameObject.SetActive(false);
    }

    private void Update()
    {
        if ((object)smelter == null) return;

        if (smelter == null || player == null || !smelter.CanInteract(player.position))
        {
            Close();
            return;
        }
        RefreshProgress();
    }

    private void OnInventoryOpenStateChanged(bool open)
    {
        if (!open) Close();
    }

    // 다른 창(재련기·상자)이 인벤토리와 함께 열리면 이 창은 닫음
    private void OnExternalChanged()
    {
        if ((object)smelter != null && inventoryUI.External != (ISlotContainer)smelter) Close();
    }

    private void BindSlots(InventorySlotUI[] slotUIs, int count, int offset)
    {
        for (int i = 0; i < slotUIs.Length; i++)
        {
            bool show = i < count;
            slotUIs[i].gameObject.SetActive(show);
            if (!show) continue;

            slotUIs[i].Bind(inventoryUI, smelter, offset + i);
            slotUIs[i].Set(smelter.Get(offset + i));
        }
    }

    private void RefreshSlot(int index)
    {
        InventorySlotUI slotUI = index < smelter.InputSlots
            ? At(inputSlotUIs, index)
            : At(outputSlotUIs, index - smelter.InputSlots);
        if (slotUI != null) slotUI.Set(smelter.Get(index));
    }

    private static InventorySlotUI At(InventorySlotUI[] array, int index)
    {
        return index >= 0 && index < array.Length ? array[index] : null;
    }

    private void RefreshProgress()
    {
        progressFill.fillAmount = smelter.Progress;

        // 레시피가 바뀔 때만 글자를 다시 만듦
        SmeltingRecipe recipe = smelter.Current;
        if (hasShownRecipe && recipe == shownRecipe) return;
        hasShownRecipe = true;
        shownRecipe = recipe;
        recipeText.text = recipe != null ? RecipeText(recipe) : idleText;
    }

    // 예: "강철 주괴 · 30초", 결과물이 여럿이면 "철 주괴 + 슬래그 ×2 · 20초"
    private static string RecipeText(SmeltingRecipe recipe)
    {
        var text = new StringBuilder();
        foreach (Ingredient result in recipe.results)
        {
            if (result.item == null) continue;
            if (text.Length > 0) text.Append(" + ");
            text.Append(result.item.displayName);
            if (result.count > 1) text.Append(" ×").Append(result.count);
        }
        text.Append($" · {recipe.seconds:0.#}초");
        return text.ToString();
    }
}
