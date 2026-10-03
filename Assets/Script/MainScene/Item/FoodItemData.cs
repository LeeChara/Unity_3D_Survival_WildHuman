using UnityEngine;

// 먹으면 허기와 체력을 회복하는 아이템 (가득 차 있어도 먹을 수 있음)
[CreateAssetMenu(fileName = "FoodItemData", menuName = "Item/FoodItemData")]
public class FoodItemData : ItemData
{
    [Tooltip("먹었을 때 회복하는 허기")]
    [Min(0f)] public float hungerAmount = 10f;
    [Tooltip("먹었을 때 회복하는 체력")]
    [Min(0f)] public float healAmount;

    // 회복량이 0인 항목은 생략
    public override string GetStatText()
    {
        string hunger = hungerAmount > 0f ? $"허기 +{hungerAmount:0.#}" : string.Empty;
        string heal = healAmount > 0f ? $"체력 +{healAmount:0.#}" : string.Empty;

        if (hunger.Length > 0 && heal.Length > 0) return $"{hunger}\n{heal}";
        return hunger + heal;
    }

    public override bool TryUse(ItemUseContext context)
    {
        if (!context.user.TryGetComponent(out PlayerHunger hunger)) return false;

        hunger.Eat(hungerAmount);
        if (healAmount > 0f && context.user.TryGetComponent(out Health health))
        {
            health.Heal(healAmount);
        }
        return true;
    }
}
