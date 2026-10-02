using System;
using System.Collections.Generic;
using UnityEngine;

// 레시피 제작 (재료 확인·소모 → 결과물 추가)과 근처 제작 스테이션 감지
public class PlayerCrafter : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [Tooltip("근처 제작 스테이션을 다시 확인하는 간격 (초)")]
    [SerializeField, Min(0.02f)] private float stationCheckInterval = 0.2f;

    // 근처에 있는 스테이션 종류 (스테이션의 stationItem)
    private readonly HashSet<ItemData> nearbyStations = new();
    private readonly HashSet<ItemData> scanBuffer = new();
    private float nextStationCheck;

    // 근처 스테이션 구성이 바뀌면 알림 (제작 가능 여부 갱신용)
    public event Action StationsChanged;
    // 제작에 성공한 레시피
    public event Action<Recipe> Crafted;

    public Inventory Inventory => inventory;

    private void Update()
    {
        if (Time.time < nextStationCheck) return;
        nextStationCheck = Time.time + stationCheckInterval;

        scanBuffer.Clear();
        foreach (CraftingStation station in CraftingStation.Active)
        {
            if (station.StationItem != null && station.IsInRange(transform.position))
            {
                scanBuffer.Add(station.StationItem);
            }
        }

        if (scanBuffer.SetEquals(nearbyStations)) return;

        nearbyStations.Clear();
        nearbyStations.UnionWith(scanBuffer);
        StationsChanged?.Invoke();
    }

    public bool HasStation(Recipe recipe)
    {
        return recipe.requiredStation == null || nearbyStations.Contains(recipe.requiredStation);
    }

    public bool HasIngredients(Recipe recipe)
    {
        foreach (Ingredient ingredient in recipe.ingredients)
        {
            if (inventory.CountOf(ingredient.item) < ingredient.count) return false;
        }
        return true;
    }

    public bool CanCraft(Recipe recipe)
    {
        return recipe != null && recipe.result != null && HasStation(recipe) && HasIngredients(recipe);
    }

    public bool TryCraft(Recipe recipe)
    {
        if (!CanCraft(recipe)) return false;

        foreach (Ingredient ingredient in recipe.ingredients)
        {
            inventory.RemoveItem(ingredient.item, ingredient.count);
        }

        // 인벤토리가 가득 차서 못 넣은 만큼은 발밑에 떨어뜨림
        int added = inventory.TryAdd(recipe.result, recipe.resultCount);
        int remaining = recipe.resultCount - added;
        if (remaining > 0)
        {
            ItemDropper.Instance.Drop(recipe.result, remaining, transform.position);
        }

        Crafted?.Invoke(recipe);
        return true;
    }
}
