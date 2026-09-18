using UnityEngine;

[CreateAssetMenu(fileName = "RoseviperData", menuName = "Monster/RoseviperData")]
public class RoseviperData : MonsterData
{
    [Header("공격")]
    public float attackHitDuration = 0.5f;

    [Header("후퇴")]
    public float retreatSpeed = 6f;
}
