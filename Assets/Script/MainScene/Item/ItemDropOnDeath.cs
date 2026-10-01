using UnityEngine;

// Health가 0이 되는 시점(Died)에 드랍 테이블대로 아이템을 떨어뜨림
// 자원 Prop과 몬스터가 함께 사용 (몬스터는 MonsterAI가 MonsterData.drops로 덮어씀)
[RequireComponent(typeof(Health))]
public class ItemDropOnDeath : MonoBehaviour
{
    [Tooltip("몬스터는 MonsterData.drops로 덮어쓰므로 여기서 설정하지 않음")]
    [SerializeField] private ItemDrop[] drops;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    // 풀링되는 Prop은 재사용될 때마다 다시 구독됨
    private void OnEnable()
    {
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Died -= OnDied;
    }

    public void SetDrops(ItemDrop[] drops)
    {
        this.drops = drops;
    }

    // ResourceHealth는 Died 이후에 풀로 반환되므로 이 시점의 위치는 아직 유효함
    private void OnDied()
    {
        if (drops == null || drops.Length == 0) return;

        if (ItemDropper.Instance == null)
        {
            Debug.LogWarning($"[ItemDropOnDeath] 씬에 ItemDropper가 없음: {name}", this);
            return;
        }

        ItemDropper.Instance.Drop(drops, transform.position);
    }
}
