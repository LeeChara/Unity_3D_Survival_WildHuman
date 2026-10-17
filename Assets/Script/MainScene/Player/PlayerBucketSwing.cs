using UnityEngine;

// 양동이를 휘두르면 물을 뜨거나 쏟음
// - 물 양동이: 휘두르면 물을 쏟아 빈 양동이가 됨 (맞힌 대상이 없어도)
// - 빈 양동이: 휘두른 범위에 물 칸이 닿으면 물을 떠서 물 양동이가 됨 (닿지 않으면 평범한 약한 공격)
// 판정이 켜질 때 정하고, 판정이 끝날 때 바꿈 (휘두르는 동안은 원래 양동이를 든 모습 유지)
// 판정이 켜지기 전(준비 동작)에 끊기면 아무것도 바뀌지 않음
public class PlayerBucketSwing : MonoBehaviour
{
    [SerializeField] private PlayerAttack attack;
    [SerializeField] private PlayerHotbar hotbar;
    [SerializeField] private Inventory inventory;
    [Tooltip("휘두르는 범위 (물 칸이 닿는지 확인)")]
    [SerializeField] private BoxCollider hitbox;
    [SerializeField] private WaterSetting waterSetting;

    // 이번 휘두르기에 든 양동이와 그 칸, 끝나면 바뀔 양동이 (없으면 null)
    private BucketItemData swinging;
    private BucketItemData result;
    private int slotIndex;

    private void OnEnable()
    {
        attack.AttackStarted += OnAttackStarted;
        attack.AttackEnded += OnAttackEnded;
    }

    private void OnDisable()
    {
        attack.AttackStarted -= OnAttackStarted;
        attack.AttackEnded -= OnAttackEnded;
        swinging = result = null;
    }

    private void OnAttackStarted()
    {
        swinging = result = null;

        ItemStack stack = hotbar.SelectedStack;
        if (stack.IsEmpty || stack.item is not BucketItemData bucket || bucket.counterpart == null) return;

        swinging = bucket;
        slotIndex = hotbar.SelectedIndex;
        if (bucket.filled || TouchesWater()) result = bucket.counterpart;

        if (result != null && SfxSetting.Instance != null)
        {
            Sfx.Play(bucket.filled ? SfxSetting.Instance.bucketPour : SfxSetting.Instance.bucketScoop, transform.position);
        }
    }

    // 휘두르는 도중 칸을 옮겼으면 원래 칸에 그 양동이가 그대로 있을 때만 바꿈
    private void OnAttackEnded()
    {
        if (swinging != null && result != null && inventory.Get(slotIndex).item == swinging)
        {
            inventory.Replace(slotIndex, result);
        }
        swinging = result = null;
    }

    // 휘두르는 범위의 가운데·네 모서리·앞쪽 가장자리 가운데 중 하나라도 물 칸인지 (지면 높이에서)
    private bool TouchesWater()
    {
        if (hitbox == null || waterSetting == null) return false;

        Vector3 c = hitbox.center;
        Vector3 h = hitbox.size * 0.5f;
        float groundY = transform.position.y;
        Vector3[] samples =
        {
            new(c.x, c.y, c.z),
            new(c.x - h.x, c.y, c.z - h.z), new(c.x + h.x, c.y, c.z - h.z),
            new(c.x - h.x, c.y, c.z + h.z), new(c.x + h.x, c.y, c.z + h.z),
            new(c.x, c.y, c.z + h.z),
        };

        foreach (Vector3 local in samples)
        {
            Vector3 point = hitbox.transform.TransformPoint(local);
            point.y = groundY;
            if (HazardQuery.IsInside(point, waterSetting.waterLayers)) return true;
        }
        return false;
    }
}
