using System;
using UnityEngine;

// 특정 도구로 공격받으면 피해가 배율만큼 늘어나는 약점 (자원·설치물이 공통으로 사용)
[Serializable]
public struct ToolWeakness
{
    [Tooltip("배율이 적용되는 도구 (None이면 배율 없음)")]
    public ToolType tool;
    [Min(1f)] public float multiplier;

    // 체력은 정수 피해로 깎이므로 배율을 곱한 결과는 반올림
    public int CalculateDamage(AttackData attack)
    {
        if (tool != ToolType.None && attack.toolType == tool)
        {
            return Mathf.RoundToInt(attack.damage * multiplier);
        }
        return attack.damage;
    }
}
