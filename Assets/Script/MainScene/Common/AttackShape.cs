using System;
using System.Collections.Generic;
using UnityEngine;

// 바닥 기준 공격 범위 모양 (범위 표시와 피격 판정이 같은 모양을 씀)
// 기준점(origin)과 정면(forward)은 공격할 때 정함. 직선은 기준점에서 정면으로 뻗음
[Serializable]
public struct AttackShape
{
    public enum Kind { Circle, Sector, Donut, Line }

    public Kind kind;
    [Tooltip("원·부채꼴·도넛의 바깥 반지름 (m)")]
    [Min(0f)] public float radius;
    [Tooltip("도넛의 안쪽 반지름 (m)")]
    [Min(0f)] public float innerRadius;
    [Tooltip("부채꼴의 전체 각도 (도)")]
    [Range(1f, 360f)] public float angle;
    [Tooltip("직선의 폭 (m)")]
    [Min(0f)] public float width;
    [Tooltip("직선의 길이 (m)")]
    [Min(0f)] public float length;

    public static AttackShape Circle(float radius) => new AttackShape { kind = Kind.Circle, radius = radius, angle = 360f };
    public static AttackShape Sector(float radius, float angle) => new AttackShape { kind = Kind.Sector, radius = radius, angle = angle };
    public static AttackShape Donut(float innerRadius, float radius) => new AttackShape { kind = Kind.Donut, radius = radius, innerRadius = innerRadius, angle = 360f };
    public static AttackShape Line(float width, float length) => new AttackShape { kind = Kind.Line, width = width, length = length };

    public bool IsLine => kind == Kind.Line;
    public float Inner => kind == Kind.Donut ? Mathf.Min(innerRadius, radius) : 0f;
    public float HalfAngle => kind == Kind.Sector ? angle * 0.5f : 180f;
    // 판정 후보를 모으는 구의 반지름
    public float Reach => IsLine ? Mathf.Sqrt(length * length + width * width * 0.25f) : radius;

    // 바닥 위의 점이 범위 안인지. margin만큼 넓혀서 봄 (대상 몸집 반영)
    public bool Contains(Vector3 origin, Vector3 forward, Vector3 point, float margin = 0f)
    {
        Vector3 to = point - origin;
        to.y = 0f;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

        if (IsLine)
        {
            float along = Vector3.Dot(to, forward);
            float side = Vector3.Dot(to, Vector3.Cross(Vector3.up, forward));
            return along >= -margin && along <= length + margin && Mathf.Abs(side) <= width * 0.5f + margin;
        }

        float distance = to.magnitude;
        if (distance > radius + margin) return false;
        if (distance < Inner - margin) return false;
        if (kind != Kind.Sector || distance < 0.01f) return true;
        // 부채꼴 옆변도 몸집만큼 넓혀서 봄
        float angleMargin = distance > margin ? Mathf.Asin(Mathf.Clamp01(margin / distance)) * Mathf.Rad2Deg : 90f;
        return Vector3.Angle(forward, to) <= HalfAngle + angleMargin;
    }

    // 범위 안의 체력 보유 대상 (콜라이더가 여럿이어도 대상당 한 번)
    public List<Health> FindTargets(Vector3 origin, Vector3 forward, LayerMask layers, Transform ignore = null)
    {
        var result = new List<Health>();
        Collider[] hits = Physics.OverlapSphere(origin, Reach + 2f, layers, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            Health health = hit.GetComponentInParent<Health>();
            if (health == null || result.Contains(health)) continue;
            if (ignore != null && health.transform == ignore) continue;

            Bounds bounds = hit.bounds;
            float margin = Mathf.Min(bounds.extents.x, bounds.extents.z);
            if (Contains(origin, forward, bounds.center, margin)) result.Add(health);
        }
        return result;
    }
}
