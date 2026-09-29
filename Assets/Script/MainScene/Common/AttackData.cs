using System;

// 공격 한 종류의 수치 (공격 종류마다 하나씩 정의)
[Serializable]
public struct AttackData
{
    public int damage;
    public float knockbackDistance;
    // 도구 장비로 공격할 때 설정 (맞는 자원에 배율 적용)
    public ToolType toolType;
}
