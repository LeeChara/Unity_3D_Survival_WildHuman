using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 인벤토리 하단 중앙의 제작 창
// 아래 줄의 가운데 칸이 선택한 레시피이고, 인벤토리 위에서 휠을 굴리거나 칸을 클릭하면 선택이 넘어감
// 위쪽에는 선택한 레시피의 재료(왼쪽)와 이름·제작 버튼(오른쪽)을 표시
public class CraftingUI : MonoBehaviour
{
    // 이 스크립트는 항상 활성 상태인 부모에 두고, 패널만 켜고 끔 (인벤토리와 같이 열림)
    [SerializeField] private GameObject panel;

    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private PlayerCrafter crafter;
    [SerializeField] private RecipeBook recipeBook;

    [Tooltip("왼쪽부터 순서대로 배치. 가운데 칸이 선택한 레시피 (홀수 개)")]
    [SerializeField] private RecipeSlotUI[] recipeSlots;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("재료 표시 줄 (레시피의 재료 수보다 많은 줄은 숨김)")]
    [SerializeField] private IngredientUI[] ingredientUIs;
    [Tooltip("필요한 제작 스테이션 이름 표시 (없어도 됨). 스테이션이 필요 없는 레시피면 숨김")]
    [SerializeField] private TMP_Text stationText;
    [Tooltip("{0}에 스테이션 아이템 이름이 들어감")]
    [SerializeField] private string stationFormat = "{0} 필요";
    [SerializeField] private Color stationNearColor = Color.white;
    [SerializeField] private Color stationFarColor = new(1f, 0.35f, 0.35f);
    [SerializeField] private Button craftButton;

    [Tooltip("끄면 지금 제작할 수 없는 레시피는 목록에서 숨김")]
    [SerializeField] private bool showUncraftable = true;

    // 지금 목록에 보이는 레시피와 그중 선택한 번호
    private readonly List<Recipe> visible = new();
    private int selected;
    private Recipe selectedRecipe;

    private int CenterSlot => recipeSlots.Length / 2;

    private void Awake()
    {
        for (int i = 0; i < recipeSlots.Length; i++)
        {
            recipeSlots[i].Bind(this, i - CenterSlot);
        }
        craftButton.onClick.AddListener(CraftSelected);
    }

    private void OnEnable()
    {
        inventoryUI.OpenStateChanged += OnOpenStateChanged;
        inventoryUI.Scrolled += OnScrolled;
        crafter.Inventory.SlotChanged += OnSlotChanged;
        crafter.StationsChanged += Refresh;
    }

    private void OnDisable()
    {
        // 재활성화 시 중복 등록되지 않도록 OnEnable과 짝을 맞춰 해제
        inventoryUI.OpenStateChanged -= OnOpenStateChanged;
        inventoryUI.Scrolled -= OnScrolled;
        crafter.Inventory.SlotChanged -= OnSlotChanged;
        crafter.StationsChanged -= Refresh;
    }

    private void Start()
    {
        // 열림 상태 이벤트는 구독 전에 지나갔을 수 있으므로 직접 한 번 반영
        OnOpenStateChanged(inventoryUI.IsOpen);
    }

    // Toggle 등 UI에서 연결해 바꿀 수 있는 설정
    public void SetShowUncraftable(bool show)
    {
        showUncraftable = show;
        Refresh();
    }

    // 선택을 delta 칸만큼 이동 (끝에서 멈춤)
    public void MoveSelection(int delta)
    {
        if (visible.Count == 0 || delta == 0) return;

        selected = Mathf.Clamp(selected + delta, 0, visible.Count - 1);
        selectedRecipe = visible[selected];
        Redraw();
    }

    private void OnOpenStateChanged(bool open)
    {
        panel.SetActive(open);
        if (open) Refresh();
    }

    // 휠을 아래로 굴리면 다음, 위로 굴리면 이전 (핫바와 같은 방향)
    private void OnScrolled(float scroll)
    {
        MoveSelection(scroll < 0f ? 1 : -1);
    }

    private void OnSlotChanged(int index)
    {
        Refresh();
    }

    // 보이는 레시피 목록을 다시 만들고 화면 갱신
    // 목록이 바뀌어도 선택하던 레시피가 남아 있으면 계속 선택
    private void Refresh()
    {
        if (!panel.activeSelf) return;

        visible.Clear();
        foreach (Recipe recipe in recipeBook.Recipes)
        {
            if (recipe == null) continue;
            if (showUncraftable || crafter.CanCraft(recipe)) visible.Add(recipe);
        }

        int keep = selectedRecipe != null ? visible.IndexOf(selectedRecipe) : -1;
        selected = keep >= 0 ? keep : Mathf.Clamp(selected, 0, Mathf.Max(visible.Count - 1, 0));
        selectedRecipe = visible.Count > 0 ? visible[selected] : null;

        Redraw();
    }

    private void Redraw()
    {
        for (int i = 0; i < recipeSlots.Length; i++)
        {
            int index = selected + i - CenterSlot;
            Recipe recipe = index >= 0 && index < visible.Count ? visible[index] : null;
            recipeSlots[i].Set(recipe, recipe != null && crafter.CanCraft(recipe));
        }

        Recipe current = selectedRecipe;
        nameText.text = current != null ? current.result.displayName : string.Empty;

        for (int i = 0; i < ingredientUIs.Length; i++)
        {
            bool show = current != null && i < current.ingredients.Length;
            ingredientUIs[i].gameObject.SetActive(show);
            if (!show) continue;

            Ingredient ingredient = current.ingredients[i];
            ingredientUIs[i].Set(ingredient.item, crafter.Inventory.CountOf(ingredient.item), ingredient.count);
        }

        if (stationText != null)
        {
            bool needStation = current != null && current.requiredStation != null;
            stationText.gameObject.SetActive(needStation);
            if (needStation)
            {
                stationText.text = string.Format(stationFormat, current.requiredStation.displayName);
                stationText.color = crafter.HasStation(current) ? stationNearColor : stationFarColor;
            }
        }

        craftButton.interactable = current != null && crafter.CanCraft(current);
    }

    private void CraftSelected()
    {
        // 재료가 바뀌면 SlotChanged로 다시 그려지므로 여기서 따로 갱신하지 않음
        crafter.TryCraft(selectedRecipe);
    }
}
