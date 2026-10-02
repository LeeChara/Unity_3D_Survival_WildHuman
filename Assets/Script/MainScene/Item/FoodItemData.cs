using UnityEngine;

// 먹으면 허기와 체력을 회복하는 아이템 (가득 차 있어도 먹을 수 있음)
[CreateAssetMenu(fileName = "FoodItemData", menuName = "Item/FoodItemData")]
public class FoodItemData : ItemData
{
    [Tooltip("먹었을 때 회복하는 허기")]
    [Min(0f)] public float hungerAmount = 10f;
    [Tooltip("먹었을 때 회복하는 체력")]
    [Min(0f)] public float healAmount;

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
