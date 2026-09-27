using System;

// 공격 한 종류의 수치 (공격 종류마다 하나씩 정의)
[Serializable]
public struct AttackData
{
    public int damage;
    public float knockbackDistance;
}
