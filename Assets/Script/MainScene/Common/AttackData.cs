using System;

// 공격 한 종류의 수치 (공격 종류마다 하나씩 정의)
[Serializable]
public struct AttackData
{
    public int damage;
    public float knockbackDistance;
    // 도구 장비로 공격할 때 설정 (맞는 자원에 배율 적용)
    public ToolType toolType;
    // 도구 티어 (맨손·몬스터는 0, 자원의 요구 티어와 비교)
    public int toolTier;
    // 공격 속성 (예: 랜턴 → 화염)
    public ElementType element;
}
