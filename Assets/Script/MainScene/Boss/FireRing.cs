using System.Collections.Generic;
using UnityEngine;

// 둥지 외곽을 두르는 불의 고리 (입구 방향은 비워 둠)
// 고리를 이루는 불길에 닿으면 화염 피해와 함께 고리 선에서 멀어지는 쪽으로 밀려남 (안에 있으면 안쪽, 밖에 있으면 바깥쪽)
public class FireRing : MonoBehaviour
{
    private readonly List<GroundFlame> flames = new();
    private Vector3 center;
    private float radius;

    // entrance: 입구가 향하는 방향, gapAngle: 비워 둘 각도 (도)
    // spacing: 불길 사이 간격 (m), lightEvery: 몇 개마다 빛을 켤지 (광원이 너무 많아지지 않도록)
    // knockback: 불길에 닿았을 때 밀려나는 거리
    public static FireRing Spawn(GroundFlame prefab, Vector3 center, float radius, Vector3 entrance, float gapAngle,
        float spacing, int lightEvery, float knockback, Transform owner)
    {
        var ring = new GameObject("FireRing").AddComponent<FireRing>();
        ring.center = new Vector3(center.x, 0f, center.z);
        ring.radius = radius;

        entrance.y = 0f;
        if (entrance.sqrMagnitude < 0.0001f) entrance = Vector3.back;
        float entranceAngle = Mathf.Atan2(entrance.x, entrance.z) * Mathf.Rad2Deg;

        int count = Mathf.Max(3, Mathf.RoundToInt(2f * Mathf.PI * radius / Mathf.Max(0.5f, spacing)));
        for (int i = 0; i < count; i++)
        {
            float angle = 360f * i / count;
            if (Mathf.Abs(Mathf.DeltaAngle(angle, entranceAngle)) < gapAngle * 0.5f) continue;

            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
            bool withLight = lightEvery > 0 && i % lightEvery == 0;
            GroundFlame flame = GroundFlame.Spawn(prefab, ring.center + offset, 0f, owner, withLight);
            flame.transform.SetParent(ring.transform, true);
            if (flame.Hazard != null)
            {
                flame.Hazard.PushDirection = ring.PushAway;
                flame.Hazard.KnockbackDistance = knockback;
            }
            ring.flames.Add(flame);
        }
        return ring;
    }

    private Vector3 PushAway(Vector3 position)
    {
        Vector3 fromCenter = position - center;
        fromCenter.y = 0f;
        if (fromCenter.sqrMagnitude < 0.0001f) return Vector3.forward;
        return fromCenter.magnitude < radius ? -fromCenter : fromCenter;
    }

    // 빛을 켠 불길에서만 타는 소리를 반복 (불길이 많아 모두 틀면 너무 시끄러움)
    public void SetLoopSfx(SfxCue cue)
    {
        if (cue == null) return;
        foreach (GroundFlame flame in flames)
        {
            if (flame != null && flame.HasLight) SfxLoop.Ensure(flame.gameObject, cue).Play();
        }
    }

    // 모든 불길을 끄고 고리를 없앰
    public void Extinguish()
    {
        foreach (GroundFlame flame in flames)
        {
            if (flame != null) flame.Extinguish();
        }
        flames.Clear();
        Destroy(gameObject, 2f);
    }
}
