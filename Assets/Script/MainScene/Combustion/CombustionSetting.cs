using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

// 연소 공용 규칙 (불이 옮겨붙는 거리·간격, 형태 변환표)
// 연소 = 탈 수 있는 대상(Combustible) + 화염 속성 → 지속 피해, 연소 중에 쓰러지면 드랍이 구운 형태로 바뀜
[CreateAssetMenu(fileName = "CombustionSetting", menuName = "Common/CombustionSetting")]
public class CombustionSetting : ScriptableObject
{
    [Header("번짐")]
    [Tooltip("불이 옮겨붙는 거리 (m, 화염원과 대상의 몸체 가장자리 사이 거리. 0이면 맞닿아야 옮겨붙음)")]
    [Min(0f)] public float spreadDistance = 1f;
    [Tooltip("화염원이 주변에 불을 옮기는 간격 (초)")]
    [Min(0.1f)] public float spreadInterval = 1f;
    [Tooltip("불이 옮겨붙는 대상 레이어 (플레이어, 몬스터, 자원, 설치물)")]
    public LayerMask targetLayers;

    [Header("형태 변환")]
    [Tooltip("연소 중에 쓰러진 대상의 드랍을 바꾸는 표 (모닥불 굽기 레시피와 같음)")]
    public CookingBook cookingBook;
    [Tooltip("불타 쓰러진 설치물을 재료로 분해할 때 쓰는 제작 레시피")]
    public RecipeBook recipeBook;

    [Header("소실")]
    [Tooltip("불에 타며 형태가 바뀔 때 일부가 재가 되는 재료와 남는 비율 (목록에 없으면 손실 없음, 예: 나무 0.5 → 숯이 절반)")]
    public List<BurnYield> burnYields = new();
    [Tooltip("불타 부서진 보관함 속 아이템이 한 개씩 사라질 확률 (남은 것은 구워짐)")]
    [Range(0f, 1f)] public float storedItemLossChance = 0.3f;

    [Serializable]
    public class BurnYield
    {
        public ItemData item;
        [Range(0f, 1f)] public float ratio = 0.5f;
    }

    // 구울 수 있으면 구운 결과, 아니면 그대로
    public ItemData Cook(ItemData item)
    {
        CookingRecipe recipe = cookingBook != null ? cookingBook.Find(item) : null;
        return recipe != null ? recipe.result : item;
    }

    // 형태가 바뀌며 남는 개수 (확률 반올림: 3 × 0.5 = 1.5 → 50% 확률로 1 또는 2)
    public int ApplyYield(ItemData item, int count)
    {
        foreach (BurnYield entry in burnYields)
        {
            if (entry.item == item) return RoundRandom(count * entry.ratio);
        }
        return count;
    }

    // 보관함 속 아이템 중 타지 않고 남는 개수 (한 개씩 판정)
    public int SurvivingCount(int count)
    {
        int survived = 0;
        for (int i = 0; i < count; i++)
        {
            if (Random.value >= storedItemLossChance) survived++;
        }
        return survived;
    }

    private static int RoundRandom(float value)
    {
        int whole = Mathf.FloorToInt(value);
        return whole + (Random.value < value - whole ? 1 : 0);
    }

    // 설치물 아이템을 만드는 제작 레시피의 재료 (결과 1개 기준, 소수점 이하 버림). 레시피가 없으면 null
    public List<Ingredient> IngredientsOf(ItemData item)
    {
        if (recipeBook == null || item == null) return null;

        foreach (Recipe recipe in recipeBook.Recipes)
        {
            if (recipe.result != item || recipe.ingredients == null) continue;

            var result = new List<Ingredient>();
            foreach (Ingredient ingredient in recipe.ingredients)
            {
                int count = ingredient.count / Mathf.Max(1, recipe.resultCount);
                if (ingredient.item != null && count > 0) result.Add(new Ingredient { item = ingredient.item, count = count });
            }
            return result;
        }
        return null;
    }
}
