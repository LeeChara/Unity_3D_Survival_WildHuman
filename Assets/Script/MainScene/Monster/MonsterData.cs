using UnityEngine;

[CreateAssetMenu(fileName = "MonsterData", menuName = "Monster/MonsterData")]
public class MonsterData : ScriptableObject
{
    public string monsterName;

    [Header("적대 대상")]
    public MonsterData[] hostileTargets;

    [Header("기본 능력치")]
    public float maxHealth;

    [Header("Idle")]
    public float moveSpeed = 1f;
    public float wanderRange = 3f;
    public float wanderCooldown = 3f;

    [Header("Chase")]
    public float chaseSpeed = 3f;
    public float chaseRange = 10f;

    [Header("Windup")]
    public float windupRange = 2f;
    public float windupDuration = 1f;

    [Header("Attack")]
    public float attackSpeed = 6f;
    public float attackDuration = 0.5f;
    public float attackDamage = 10f;

    [Header("Recover")]
    public float recoverDuration = 1f;
}
