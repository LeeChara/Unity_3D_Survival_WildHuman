using UnityEngine;

[CreateAssetMenu(fileName = "MonsterData", menuName = "Monster/MonsterData")]
public class MonsterData : ScriptableObject
{
    public string monsterName;
    [Tooltip("마우스를 올렸을 때 표시할 이름 (비워두면 표시하지 않음)")]
    public string displayName;
    [Tooltip("도감에 표시할 설명 (특징, 행동 패턴 등)")]
    [TextArea] public string description;
    [Tooltip("도감 등 UI에 표시할 전신 이미지 (몬스터 프리팹은 부위별 스프라이트로 나뉘어 있음)")]
    public Sprite portrait;

    [Header("변종")]
    [Tooltip("바이옴 변종이면 원본 종 (비워두면 자기 자신이 원본). 적대 관계는 원본 종 기준으로 판정")]
    public MonsterData species;

    [Header("적대 대상")]
    [Tooltip("원본 종으로 지정 (그 종의 변종도 모두 적대 대상이 됨)")]
    public MonsterData[] hostileTargets;

    [Header("기본 능력치")]
    public float maxHealth;
    [Tooltip("체력 비율이 이 값 이하가 되면 땀방울 이펙트 표시")]
    [Range(0f, 1f)] public float lowHealthRatio = 0.3f;

    [Header("Idle")]
    public float moveSpeed = 1f;
    public float wanderRange = 3f;
    public float wanderCooldown = 3f;

    [Header("Chase")]
    public float chaseSpeed = 3f;
    [Tooltip("목표물 감지 반경")]
    public float chaseRange = 10f;
    [Tooltip("추격 포기 반경. 감지 반경보다 넓어야 경계에서 인식/해제가 반복되지 않음")]
    public float chaseGiveUpRange = 15f;

    [Header("Windup")]
    public float windupRange = 2f;
    public float windupDuration = 1f;

    [Header("Attack")]
    public float attackSpeed = 6f;
    public float attackDuration = 0.5f;
    public AttackData attack = new AttackData { damage = 10, knockbackDistance = 1f };

    [Header("Recover")]
    public float recoverDuration = 1f;

    [Header("Knockback")]
    [Tooltip("피격 시 덜 밀리는 정도 (1이면 밀리지 않음)")]
    [Range(0f, 1f)] public float knockbackResistance = 0f;
    [Tooltip("Windup/Attack 중 피격 시 넉백되지 않고 공격을 유지")]
    public bool superArmorWhileAttacking = false;

    [Header("Drop")]
    [Tooltip("사망 시 드랍 (ItemDropOnDeath가 있는 프리팹에만 적용)")]
    public DropTable dropTable;

    public MonsterData Species => species != null ? species : this;

    // 변종끼리도 원본 종이 같으면 같은 대상으로 봄
    public bool IsHostileTo(MonsterData other)
    {
        if (other == null || hostileTargets == null) return false;

        MonsterData otherSpecies = other.Species;
        foreach (MonsterData hostileTarget in hostileTargets)
        {
            if (hostileTarget != null && hostileTarget.Species == otherSpecies) return true;
        }
        return false;
    }

    private void OnValidate()
    {
        // 포기 반경이 감지 반경보다 좁으면 감지 직후 바로 포기하게 되므로 보정
        chaseGiveUpRange = Mathf.Max(chaseGiveUpRange, chaseRange);
    }
}
