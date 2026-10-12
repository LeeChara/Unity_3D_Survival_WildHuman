using System.Collections.Generic;
using UnityEngine;

// 금속 물체(Resonant)에서 진행 방향으로 퍼지는 부채꼴 음파
// spreadTime 동안 앞으로 퍼지며, 파면이 지나간 대상(ISoundReactive)마다 한 번씩 반응을 일으킴 (피해 없음)
// 파면이 다른 구조물(blockLayers)에 닿으면 그 거리에서 음파 전체가 멈추고 사라짐
// 닿은 구조물이 금속이면 음파를 받아 다시 울림 (연쇄)
public class SoundWave : MonoBehaviour
{
    private const int FrameCount = 8;
    // 구조물에 막혀 사라지는 데 걸리는 시간 (초)
    private const float BlockedFadeTime = 0.12f;

    public SoundWaveSetting Setting { get; private set; }
    public Vector3 Origin { get; private set; }
    // 수평 단위 벡터
    public Vector3 Direction { get; private set; }
    // 첫 음파 1, 연쇄될 때마다 감쇠
    public float Power { get; private set; }
    // 연쇄 단계 (첫 음파 0)
    public int Depth { get; private set; }
    // 음파를 낸 물체 (자신에게는 반응·막힘을 적용하지 않음)
    public Transform Source { get; private set; }

    // 세기를 반영한 최대 거리
    public float Length => Setting.length * Power;

    private float time;
    private readonly HashSet<ISoundReactive> reached = new();
    private SpriteRenderer ring;
    private Sprite[] frames;

    // 구조물에 막힌 뒤 사라지는 중이면 남은 시간 (막히지 않았으면 음수)
    private float blockedFade = -1f;
    private float blockedAlpha;
    // 지금 표시 중인 진하기 (세기 반영 전)
    private float alpha;

    // 반응 중에 새 음파가 생겨도 조회는 다음 프레임부터라 공용 버퍼를 덮어쓰지 않음
    private static readonly Collider[] overlapBuffer = new Collider[128];
    private static readonly List<(Collider collider, float distance)> hits = new();
    private static readonly Dictionary<(int, int, int, byte), Sprite[]> frameCache = new();

    public static SoundWave Spawn(SoundWaveSetting setting, Vector3 origin, Vector3 direction, float power, int depth, Transform source)
    {
        var go = new GameObject($"SoundWave (단계 {depth})");
        go.transform.position = origin;

        var wave = go.AddComponent<SoundWave>();
        wave.Setting = setting;
        wave.Origin = origin;
        wave.Direction = direction;
        wave.Power = power;
        wave.Depth = depth;
        wave.Source = source;
        wave.CreateVisual();
        return wave;
    }

    private void Update()
    {
        // 막힌 뒤에는 멈춘 자리에서 빠르게 흐려짐
        if (blockedFade >= 0f)
        {
            blockedFade -= Time.deltaTime;
            SetAlpha(blockedAlpha * Mathf.Clamp01(blockedFade / BlockedFadeTime));
            if (blockedFade <= 0f) Destroy(gameObject);
            return;
        }

        time += Time.deltaTime;
        float progress = Mathf.Clamp01(time / Setting.spreadTime);
        float reach = Length * progress;

        float stop = Propagate(reach);
        if (stop < reach)
        {
            ShowReach(stop);
            blockedAlpha = alpha;
            blockedFade = BlockedFadeTime;
            return;
        }

        ShowReach(reach);
        if (progress >= 1f) Destroy(gameObject);
    }

    // reach까지 퍼뜨리고, 구조물에 막혔으면 막힌 거리를 반환 (막히지 않았으면 무한대)
    // 막힌 거리까지의 대상(막은 구조물 포함)에게만 반응을 일으킴
    private float Propagate(float reach)
    {
        if (reach <= 0f) return float.PositiveInfinity;

        int mask = Setting.targetLayers.value | Setting.blockLayers.value;
        int count = Physics.OverlapSphereNonAlloc(Origin, reach, overlapBuffer, mask, QueryTriggerInteraction.Ignore);

        hits.Clear();
        float stop = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider c = overlapBuffer[i];
            if (Source != null && c.transform.IsChildOf(Source)) continue;
            if (!TryGetDistanceInFan(c, reach, out float distance)) continue;

            hits.Add((c, distance));
            if ((Setting.blockLayers.value & (1 << c.gameObject.layer)) != 0) stop = Mathf.Min(stop, distance);
        }

        float limit = Mathf.Min(reach, stop);
        foreach (var (c, distance) in hits)
        {
            if (distance > limit + 0.01f) continue;

            ISoundReactive reactive = c.GetComponentInParent<ISoundReactive>();
            if (reactive == null || !reached.Add(reactive)) continue;
            reactive.OnSoundWave(this);
        }
        return stop;
    }

    // 몸체에서 시작점에 가장 가까운 점, 그게 부채꼴 밖이면 중심으로 한 번 더 확인
    private bool TryGetDistanceInFan(Collider c, float reach, out float distance)
    {
        Bounds bounds = c.bounds;
        if (IsInFan(bounds.ClosestPoint(Origin), reach, out distance)) return true;
        return IsInFan(bounds.center, reach, out distance);
    }

    private bool IsInFan(Vector3 point, float reach, out float distance)
    {
        Vector3 to = point - Origin;
        to.y = 0f;
        distance = to.magnitude;
        if (distance > reach) return false;
        // 시작점과 겹치면 방향을 따질 수 없으므로 닿은 것으로 봄
        if (distance < 0.01f) return true;
        return Vector3.Angle(Direction, to) <= Setting.angle * 0.5f;
    }

    #region 표시

    // 바닥에 눕힌 부채꼴 고리 (도트 그림을 코드로 생성, 퍼진 거리에 따라 프레임 교체)
    private void CreateVisual()
    {
        frames = GetFrames(Setting);

        var go = new GameObject("Ring");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.02f;
        // 바닥에 눕힌 뒤(X 90도) 스프라이트의 위쪽(Y)이 진행 방향을 향하도록 회전
        go.transform.rotation = Quaternion.Euler(90f, Mathf.Atan2(Direction.x, Direction.z) * Mathf.Rad2Deg, 0f);
        go.transform.localScale = Vector3.one * Length;

        ring = go.AddComponent<SpriteRenderer>();
        ring.sprite = frames[0];
        // 바닥 위, 모든 스프라이트 아래 (용암 타일은 short.MinValue)
        ring.sortingOrder = short.MinValue + 1;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ShowReach(0f);
    }

    private void ShowReach(float reach)
    {
        if (ring == null) return;

        float progress = Length > 0f ? Mathf.Clamp01(reach / Length) : 1f;
        ring.sprite = frames[Mathf.Min(FrameCount - 1, Mathf.FloorToInt(progress * FrameCount))];
        // 끝부분에서 흐려짐
        SetAlpha(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, progress)));
    }

    // 연쇄로 약해진 음파일수록 옅게
    private void SetAlpha(float value)
    {
        alpha = value;
        if (ring == null) return;

        Color color = Setting.waveColor;
        color.a *= value * Mathf.Lerp(0.5f, 1f, Power);
        ring.color = color;
    }

    // 1 유닛 = 반지름 1 (Length로 스케일), 피벗은 부채꼴의 꼭짓점
    private static Sprite[] GetFrames(SoundWaveSetting setting)
    {
        int radius = setting.pixelRadius;
        int thickness = setting.ringThickness;
        int angle = Mathf.RoundToInt(setting.angle);
        byte fill = (byte)Mathf.RoundToInt(setting.fillAlpha * 255f);
        var key = (radius, thickness, angle, fill);
        if (frameCache.TryGetValue(key, out Sprite[] cached) && cached[0] != null) return cached;

        float halfAngle = angle * 0.5f;
        int width = radius * 2 + 1;
        int height = radius + 1;
        var result = new Sprite[FrameCount];

        for (int f = 0; f < FrameCount; f++)
        {
            float outer = radius * (f + 1) / (float)FrameCount;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"SoundWave_{f}",
            };
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x - radius;
                    float py = y + 0.5f;
                    float d = Mathf.Sqrt(px * px + py * py);
                    if (d > outer || Mathf.Abs(Mathf.Atan2(px, py) * Mathf.Rad2Deg) > halfAngle) continue;

                    // 바깥 고리는 진하게, 안쪽은 지나간 자리로 옅게
                    byte alpha = d >= outer - thickness ? (byte)255 : fill;
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var pivot = new Vector2((radius + 0.5f) / width, 0f);
            result[f] = Sprite.Create(tex, new Rect(0, 0, width, height), pivot, radius, 0, SpriteMeshType.FullRect);
        }

        frameCache[key] = result;
        return result;
    }

    #endregion

    private void OnDrawGizmos()
    {
        if (Setting == null) return;
        DrawFanGizmo(Origin, Direction, Setting.angle, Length);
    }

    public static void DrawFanGizmo(Vector3 origin, Vector3 direction, float angle, float length)
    {
        float half = angle * 0.5f;
        Vector3 left = Quaternion.Euler(0f, -half, 0f) * direction * length;
        Vector3 right = Quaternion.Euler(0f, half, 0f) * direction * length;
        Gizmos.color = new Color(0.6f, 0.85f, 1f, 0.9f);
        Gizmos.DrawLine(origin, origin + left);
        Gizmos.DrawLine(origin, origin + right);

        const int segments = 12;
        Vector3 prev = origin + left;
        for (int i = 1; i <= segments; i++)
        {
            Vector3 next = origin + Quaternion.Euler(0f, Mathf.Lerp(-half, half, i / (float)segments), 0f) * direction * length;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    public static void DrawCircleGizmo(Vector3 center, float radius)
    {
        const int segments = 32;
        Vector3 prev = center + Vector3.forward * radius;
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 next = center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
