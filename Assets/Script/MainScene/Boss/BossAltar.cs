using UnityEngine;

// 둥지의 제단: 보스가 없을 때 공물을 들고 우클릭하면 1개를 바치고 보스를 다시 부름
// 제단 자체는 부서지지 않는 구조물 (StructureHealth + 파괴 불가 설치물 데이터로 이름 표시)
public class BossAltar : MonoBehaviour, IInteractable
{
    [Tooltip("바쳐야 하는 아이템")]
    [SerializeField] private ItemData offering;
    [Tooltip("상호작용할 수 있는 거리 (m)")]
    [SerializeField, Min(0f)] private float range = 3f;

    private BossNest nest;

    private void Awake()
    {
        nest = GetComponentInParent<BossNest>();
    }

    private bool CanOffer(PlayerHotbar hotbar)
    {
        if (nest == null || nest.HasBoss || offering == null) return false;
        ItemStack stack = hotbar.SelectedStack;
        return !stack.IsEmpty && stack.item == offering;
    }

    public bool CanInteract(Vector3 from)
    {
        Vector3 offset = from - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= range * range;
    }

    public void Interact(Inventory inventory, PlayerHotbar hotbar)
    {
        if (!CanOffer(hotbar)) return;
        if (nest.Summon()) inventory.Remove(hotbar.SelectedIndex, 1);
    }

    public string GetInteractPreview(PlayerHotbar hotbar)
    {
        return CanOffer(hotbar) ? $"바치기 ({offering.displayName})" : null;
    }
}
