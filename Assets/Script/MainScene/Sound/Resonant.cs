using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 금속 물체 표시 (붙어 있으면 공명 가능, 예: 강철 울타리)
// 강타 공격에 맞으면 '땡' 울리고 떨림이 점점 커지다가(strikeDelay) 음파(SoundWave)를 냄. 방향은 방향 방식에 따름
// 강타가 아닌 공격이나 연쇄하기엔 약한 음파에 닿으면 작게 '땡' 울리고 살짝 떨리기만 함
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
    [Tooltip("울릴 때 떨리는 그림 (화면 가로 방향으로 흔들림)")]
    [SerializeField] private Transform[] visuals;

    public SoundWaveSetting Setting => setting;

    // 연쇄 하나는 금방 끝나므로 최근 것만 기억해도 충분
    private const int RememberedChains = 8;
    // 물체마다 음높이를 조금씩 다르게
    private const float PitchVariance = 0.06f;

    // 강타 한 번에서 시작된 연쇄마다 새 번호 (그 연쇄에서 퍼진 음파는 모두 같은 번호)
    private static int lastChainId;

    private Collider[] body;
    // 강타를 맞고 음파를 내기까지 기다리는 중 (그동안의 강타는 무시, 여러 기둥이 같이 맞아도 한 번만 울림)
    private bool pending;
    // 이미 울린 연쇄 번호 (같은 연쇄 안에서는 한 번만 울림, 음파가 금속을 지나가며 겹쳐 닿아도 중복으로 울리지 않음)
    private readonly Queue<int> rungChains = new();

    private float pitch;
    private Vector3[] visualBase;
    private bool shaking;
    // 약한 울림 떨림의 남은 시간
    private float weakShakeLeft;
    // 음파를 모으기 시작한 뒤 지난 시간
    private float chargeElapsed;

    private void Awake()
    {
        body = FindBody();
        pitch = 1f + Random.Range(-PitchVariance, PitchVariance);

        if (visuals == null) visuals = new Transform[0];
        visualBase = new Vector3[visuals.Length];
        for (int i = 0; i < visuals.Length; i++)
        {
            if (visuals[i] != null) visualBase[i] = visuals[i].localPosition;
        }
    }

    // 공격에 맞음. 강타면 새 연쇄를 시작하고, 아니면(또는 음파를 모으는 중이면) 작게 울림. from: 공격자 위치
    public void OnAttackHit(Vector3 from, bool blunt)
    {
        if (blunt && Strike(from, 1f, 0, ++lastChainId)) return;
        Ring();
    }

    public void OnSoundWave(SoundWave wave)
    {
        // 같은 연쇄의 음파가 다시 닿은 것은 조용히 무시
        if (setting == null || rungChains.Contains(wave.ChainId)) return;
        if (!Strike(wave.Origin, wave.Power * setting.chainDecay, wave.Depth + 1, wave.ChainId)) Ring();
    }

    // 음파를 모으기 시작했으면 true
    // from: 강타가 들어온 위치 (공격자 또는 앞선 음파의 시작점), power: 음파 세기 (첫 음파 1), depth: 연쇄 단계 (첫 음파 0)
    private bool Strike(Vector3 from, float power, int depth, int chainId)
    {
        if (setting == null) return false;
        if (depth > setting.maxChain || power < setting.minPower) return false;
        if (pending || rungChains.Contains(chainId)) return false;

        // 방향·시작점은 맞은 순간 기준 (기다리는 동안 공격자가 움직여도 그대로)
        Vector3 center = GetBounds().center;
        if (!TryGetDirection(center, from, out Vector3 dir)) return false;

        rungChains.Enqueue(chainId);
        if (rungChains.Count > RememberedChains) rungChains.Dequeue();

        float volume = Mathf.Lerp(0.5f, 1f, power);
        Sfx.Play(setting.ringClip, center, volume, pitch);
        Sfx.Play(setting.chargeClip, center, volume, pitch);

        pending = true;
        chargeElapsed = 0f;
        StartCoroutine(Emit(EdgePoint(center, dir), dir, power, depth, chainId));
        return true;
    }

    // 작은 '땡'과 짧은 떨림 (음파 없음)
    private void Ring()
    {
        if (setting == null) return;

        Sfx.Play(setting.ringClip, transform.position, setting.weakRingVolume, pitch);
        weakShakeLeft = setting.weakShakeTime;
    }

    // 기다리는 동안 부서지면 코루틴도 멈춰서 음파가 나가지 않음
    private IEnumerator Emit(Vector3 origin, Vector3 dir, float power, int depth, int chainId)
    {
        if (setting.strikeDelay > 0f) yield return new WaitForSeconds(setting.strikeDelay);

        pending = false;
        Sfx.Play(setting.waveClip, origin, Mathf.Lerp(0.5f, 1f, power), pitch);
        SoundWave.Spawn(setting, origin, dir, power, depth, chainId, transform);
    }

    // 꺼졌다 켜지면 멈춘 코루틴이 남긴 대기 상태를 풀어 둠
    private void OnDisable()
    {
        pending = false;
        weakShakeLeft = 0f;
        ApplyShake(0f);
    }

    private void Update()
    {
        if (setting == null) return;

        float amplitude = 0f;
        if (weakShakeLeft > 0f)
        {
            weakShakeLeft -= Time.deltaTime;
            amplitude = setting.weakShake * Mathf.Clamp01(weakShakeLeft / Mathf.Max(0.01f, setting.weakShakeTime));
        }

        // 모으는 동안 떨림이 점점 커짐
        if (pending)
        {
            chargeElapsed += Time.deltaTime;
            float t = setting.strikeDelay > 0f ? Mathf.Clamp01(chargeElapsed / setting.strikeDelay) : 1f;
            amplitude = Mathf.Max(amplitude, Mathf.Lerp(setting.weakShake, setting.chargeShake, t));
        }

        ApplyShake(amplitude);
    }

    // 그림을 화면 가로 방향(빌보드의 오른쪽)으로 좌우 번갈아 밀어냄
    private void ApplyShake(float amplitude)
    {
        if (visualBase == null) return;
        if (amplitude <= 0f && !shaking) return;
        shaking = amplitude > 0f;

        float side = Mathf.FloorToInt(Time.time * setting.shakeFrequency) % 2 == 0 ? 1f : -1f;
        for (int i = 0; i < visuals.Length; i++)
        {
            Transform visual = visuals[i];
            if (visual == null) continue;

            Vector3 offset = visual.right * (amplitude * side);
            if (visual.parent != null) offset = visual.parent.InverseTransformVector(offset);
            visual.localPosition = visualBase[i] + offset;
        }
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
