using System;
using UnityEngine;

// 제작법 한 개의 정의 (RecipeBook 에셋 한 곳에 모아서 관리)
[Serializable]
public class Recipe
{
    public ItemData result;
    [Min(1)] public int resultCount = 1;
    public Ingredient[] ingredients;

    [Tooltip("근처에 있어야 제작할 수 있는 설치물 (비우면 어디서나 제작). CraftingStation의 stationItem과 같은 아이템을 지정")]
    public ItemData requiredStation;
}

[Serializable]
public class Ingredient
{
    public ItemData item;
    [Min(1)] public int count = 1;
}
