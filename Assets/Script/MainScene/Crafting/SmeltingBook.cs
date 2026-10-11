using System.Collections.Generic;
using UnityEngine;

// 재련기 설치물이 쓰는 레시피 모음 (재료 여러 종류 → 결과물 여러 종류)
// 재료 칸이 여러 레시피를 만족하면 위에 있는 레시피가 먼저
[CreateAssetMenu(fileName = "SmeltingBook", menuName = "Crafting/SmeltingBook")]
public class SmeltingBook : ScriptableObject
{
    [Tooltip("도감 등에 표시할 작업 이름")]
    [SerializeField] private string actionName = "재련";
    [SerializeField] private SmeltingRecipe[] recipes;

    public string ActionName => actionName;
    public IReadOnlyList<SmeltingRecipe> Recipes => recipes;

    // 어떤 레시피든 재료로 쓰이는 아이템인지 (재련기 재료 칸에 넣을 수 있는지)
    public bool IsIngredient(ItemData item)
    {
        if (item == null || recipes == null) return false;

        foreach (SmeltingRecipe recipe in recipes)
        {
            if (recipe.ingredients == null) continue;
            foreach (Ingredient ingredient in recipe.ingredients)
            {
                if (ingredient.item == item) return true;
            }
        }
        return false;
    }
}
