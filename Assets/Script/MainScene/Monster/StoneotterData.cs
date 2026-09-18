using UnityEngine;

[CreateAssetMenu(fileName = "StoneotterData", menuName = "Monster/StoneotterData")]
public class StoneotterData : MonsterData
{
    [Header("후퇴 거리")]
    public float windupMinRange;

    [Header("돌멩이 투척")]
    public Projectile stonePrefab;
    public float stoneSpeed = 8f;
    public float throwInterval = 0.4f;
    public int throwCount = 3;
}