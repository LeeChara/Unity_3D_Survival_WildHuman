using System.Collections.Generic;
using UnityEngine;

// 모닥불 굽기 레시피를 한 곳에 모은 데이터 (재료 하나당 레시피 하나)
[CreateAssetMenu(fileName = "CookingBook", menuName = "Crafting/CookingBook")]
public class CookingBook : ScriptableObject
{
    [SerializeField] private CookingRecipe[] recipes;

    public IReadOnlyList<CookingRecipe> Recipes => recipes;

    // 이 재료로 구울 수 있는 레시피 (없으면 null)
    public CookingRecipe Find(ItemData input)
    {
        if (input == null) return null;

        foreach (CookingRecipe recipe in recipes)
        {
            if (recipe.input == input && recipe.result != null) return recipe;
        }
        return null;
    }
}
