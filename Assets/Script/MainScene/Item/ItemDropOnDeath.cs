using UnityEngine;

// Health가 0이 되는 시점(Died)에 드랍 테이블대로 아이템을 떨어뜨림
// 자원 Prop, 설치물, 몬스터가 함께 사용 (각각 PropData, PlaceableItemData, MonsterData의 dropTable로 덮어씀)
[RequireComponent(typeof(Health))]
public class ItemDropOnDeath : MonoBehaviour
{
    [Tooltip("자원·설치물·몬스터는 각자의 데이터 에셋으로 덮어쓰므로 여기서 설정하지 않음")]
    [SerializeField] private DropTable dropTable;

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

    public void SetDropTable(DropTable dropTable)
    {
        this.dropTable = dropTable;
    }

    // PropHealth는 Died 이후에 풀로 반환되므로 이 시점의 위치는 아직 유효함
    private void OnDied()
    {
        if (dropTable == null || dropTable.Drops == null || dropTable.Drops.Count == 0) return;

        if (ItemDropper.Instance == null)
        {
            Debug.LogWarning($"[ItemDropOnDeath] 씬에 ItemDropper가 없음: {name}", this);
            return;
        }

        ItemDropper.Instance.Drop(dropTable.Drops, transform.position);
    }
}
