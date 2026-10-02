using System.Collections.Generic;
using UnityEngine;

// 모든 제작 레시피를 한 곳에 모은 데이터 (이 순서대로 제작 창에 표시)
[CreateAssetMenu(fileName = "RecipeBook", menuName = "Crafting/RecipeBook")]
public class RecipeBook : ScriptableObject
{
    [SerializeField] private Recipe[] recipes;

    public IReadOnlyList<Recipe> Recipes => recipes;
}
