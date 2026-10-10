using System.Collections.Generic;
using UnityEngine;

// Cooker 설치물 하나가 쓰는 레시피 모음 (예: 모닥불 굽기, 재련기 재련, 재료 하나당 레시피 하나)
[CreateAssetMenu(fileName = "CookingBook", menuName = "Crafting/CookingBook")]
public class CookingBook : ScriptableObject
{
    [Tooltip("도감 등에 표시할 작업 이름 (예: 굽기, 재련)")]
    [SerializeField] private string actionName = "굽기";
    [SerializeField] private CookingRecipe[] recipes;

    public string ActionName => actionName;
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
