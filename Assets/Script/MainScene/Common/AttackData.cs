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
    // 타격 형태 (예: 강철 둔기 → 강타, 금속 물체를 때리면 음파 발생)
    public StrikeType strike;

    // 피해 없이 속성만 실린 공격 (예: 물 양동이 → 젖음만). 피격 반짝임·넉백·금속 울림 없이 속성 반응만 일어남
    public bool IsHarmless => damage <= 0 && element != ElementType.None;
}
