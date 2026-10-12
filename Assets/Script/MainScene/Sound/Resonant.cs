using UnityEngine;

// 금속 물체 표시 (붙어 있으면 공명 가능, 예: 강철 울타리)
// 강타 공격에 맞으면 음파(SoundWave)를 냄. 방향은 방향 방식에 따름
// 음파도 강타라서 다른 음파에 닿으면 다시 울림 → 연쇄 (SoundWaveSetting의 횟수 상한·감쇠로 멈춤)
[DisallowMultipleComponent]
public class Resonant : MonoBehaviour, ISoundReactive
{
    public enum WaveDirection
    {
        // 강타가 들어온 방향 그대로 반대편으로 (둥근 물체 등)
        AwayFromHit,
        // 설치 방향의 앞(로컬 Z)·뒤 중 강타가 들어온 반대쪽으로 (울타리처럼 길게 뻗은 물체, 대각선이면 대각선)
        Facing,
    }

    [SerializeField] private SoundWaveSetting setting;
    [SerializeField] private WaveDirection direction = WaveDirection.AwayFromHit;

    public SoundWaveSetting Setting => setting;

    private Collider[] body;
    // 한 번 휘두를 때 여러 콜라이더(기둥)가 같이 맞아도 한 번만 울림
    private int lastStrikeFrame = -1;

    private void Awake()
    {
        body = FindBody();
    }

    // from: 강타가 들어온 위치 (공격자 또는 앞선 음파의 시작점), power: 음파 세기 (첫 음파 1), depth: 연쇄 단계 (첫 음파 0)
    public void Strike(Vector3 from, float power, int depth)
    {
        if (setting == null) return;
        if (depth > setting.maxChain || power < setting.minPower) return;
        if (lastStrikeFrame == Time.frameCount) return;

        Vector3 center = GetBounds().center;
        if (!TryGetDirection(center, from, out Vector3 dir)) return;

        lastStrikeFrame = Time.frameCount;
        SoundWave.Spawn(setting, EdgePoint(center, dir), dir, power, depth, transform);
    }

    public void OnSoundWave(SoundWave wave)
    {
        Strike(wave.Origin, wave.Power * setting.chainDecay, wave.Depth + 1);
    }

    private bool TryGetDirection(Vector3 center, Vector3 from, out Vector3 dir)
    {
        Vector3 incoming = center - from;
        incoming.y = 0f;

        if (direction == WaveDirection.Facing)
        {
            Vector3 normal = transform.forward;
            normal.y = 0f;
            if (normal.sqrMagnitude > 0.0001f)
            {
                normal.Normalize();
                // 정확히 옆 끝에서 맞아도 앞·뒤 중 한쪽으로 정해짐
                dir = Vector3.Dot(incoming, normal) >= 0f ? normal : -normal;
                return true;
            }
        }

        dir = incoming;
        if (dir.sqrMagnitude < 0.0001f) return false;
        dir.Normalize();
        return true;
    }

    // 음파는 몸체 가장자리(진행 방향 쪽)에서 시작, 높이는 바닥
    // 상자 콜라이더는 회전을 반영해서 계산 (대각선 울타리도 정확히 앞·뒷면에서 시작)
    private Vector3 EdgePoint(Vector3 center, Vector3 dir)
    {
        float reach = 0f;
        foreach (Collider c in Body())
        {
            reach = Mathf.Max(reach, Vector3.Dot(BodyCenter(c) - center, dir) + HalfExtentAlong(c, dir));
        }

        Vector3 point = center + dir * reach;
        point.y = transform.position.y;
        return point;
    }

    private static Vector3 BodyCenter(Collider c)
    {
        return c is BoxCollider box ? box.transform.TransformPoint(box.center) : c.bounds.center;
    }

    // 몸체를 dir 방향으로 투영한 반폭
    private static float HalfExtentAlong(Collider c, Vector3 dir)
    {
        if (c is BoxCollider box)
        {
            Transform t = box.transform;
            Vector3 half = Vector3.Scale(box.size, t.lossyScale) * 0.5f;
            return Mathf.Abs(Vector3.Dot(t.right, dir)) * Mathf.Abs(half.x)
                 + Mathf.Abs(Vector3.Dot(t.up, dir)) * Mathf.Abs(half.y)
                 + Mathf.Abs(Vector3.Dot(t.forward, dir)) * Mathf.Abs(half.z);
        }

        Vector3 extents = c.bounds.extents;
        return Mathf.Abs(dir.x) * extents.x + Mathf.Abs(dir.z) * extents.z;
    }

    private Bounds GetBounds()
    {
        bool found = false;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        foreach (Collider c in Body())
        {
            if (!found) bounds = c.bounds;
            else bounds.Encapsulate(c.bounds);
            found = true;
        }
        return bounds;
    }

    private System.Collections.Generic.IEnumerable<Collider> Body()
    {
        if (body == null || body.Length == 0) body = FindBody();
        foreach (Collider c in body)
        {
            if (c != null && c.enabled) yield return c;
        }
    }

    // 트리거(접촉 범위 등)는 몸체가 아님
    private Collider[] FindBody()
    {
        var colliders = GetComponentsInChildren<Collider>();
        int count = 0;
        foreach (Collider c in colliders) if (!c.isTrigger) count++;

        var result = new Collider[count];
        int i = 0;
        foreach (Collider c in colliders) if (!c.isTrigger) result[i++] = c;
        return result;
    }

    // 음파가 나갈 수 있는 방향과 최대 거리 (첫 음파 기준)
    private void OnDrawGizmosSelected()
    {
        if (setting == null) return;

        Bounds bounds = GetBounds();
        Gizmos.color = new Color(0.6f, 0.85f, 1f, 0.8f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        if (direction == WaveDirection.Facing)
        {
            Vector3 normal = transform.forward;
            normal.y = 0f;
            normal.Normalize();
            foreach (Vector3 dir in new[] { normal, -normal })
            {
                SoundWave.DrawFanGizmo(EdgePoint(bounds.center, dir), dir, setting.angle, setting.length);
            }
            return;
        }

        SoundWave.DrawCircleGizmo(new Vector3(bounds.center.x, transform.position.y, bounds.center.z), setting.length + Mathf.Max(bounds.extents.x, bounds.extents.z));
    }
}
