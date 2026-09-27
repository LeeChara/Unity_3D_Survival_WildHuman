using UnityEngine;

[CreateAssetMenu(fileName = "MonsterData", menuName = "Monster/MonsterData")]
public class MonsterData : ScriptableObject
{
    public string monsterName;

    [Header("적대 대상")]
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

    private void OnValidate()
    {
        // 포기 반경이 감지 반경보다 좁으면 감지 직후 바로 포기하게 되므로 보정
        chaseGiveUpRange = Mathf.Max(chaseGiveUpRange, chaseRange);
    }
}
