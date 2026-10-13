using System.Collections.Generic;
using UnityEngine;

// 금속 물체(Resonant)에서 진행 방향으로 퍼지는 부채꼴 음파
// spreadSpeed로 앞으로 퍼지며, 파면이 지나간 대상(ISoundReactive)마다 한 번씩 반응을 일으킴 (피해 없음)
// 무엇에도 막히지 않고 끝까지 퍼짐. 금속 물체는 음파를 받아서 다시 울림 (연쇄) → 지나가는 동안 여러 음파로 퍼짐
public class SoundWave : MonoBehaviour
{
    private const int FrameCount = 8;

    public SoundWaveSetting Setting { get; private set; }
    public Vector3 Origin { get; private set; }
    // 수평 단위 벡터
    public Vector3 Direction { get; private set; }
    // 첫 음파 1, 연쇄될 때마다 감쇠
    public float Power { get; private set; }
    // 연쇄 단계 (첫 음파 0)
    public int Depth { get; private set; }
    // 처음 강타에서 시작된 연쇄 번호 (같은 연쇄에서 금속 물체는 한 번만 울림)
    public int ChainId { get; private set; }
    // 부채꼴 각도 (360이면 사방으로 퍼지는 원, 예: 종)
    public float Angle { get; private set; }
    // 보낸 물체의 거리 배율 (예: 종은 더 멀리 퍼짐)
    public float LengthScale { get; private set; } = 1f;
    // 보낸 물체의 속도 배율 (예: 종은 천천히 퍼짐)
    public float SpeedScale { get; private set; } = 1f;
    // 음파를 낸 물체 (자신에게는 반응을 일으키지 않음)
    public Transform Source { get; private set; }

    // 세기를 반영한 최대 거리
    public float Length => Setting.length * Power * LengthScale;

    private float time;
    private readonly HashSet<ISoundReactive> reached = new();
    private SpriteRenderer ring;
    private Sprite[] frames;

    // 반응 중에 새 음파가 생겨도 조회는 다음 프레임부터라 공용 버퍼를 덮어쓰지 않음
    private static readonly Collider[] overlapBuffer = new Collider[128];
    private static readonly Dictionary<(int, int, int, byte), Sprite[]> frameCache = new();

    public static SoundWave Spawn(SoundWaveSetting setting, Vector3 origin, Vector3 direction, float power, int depth, int chainId, Transform source, float angle = -1f, float lengthScale = 1f, float speedScale = 1f)
    {
        var go = new GameObject($"SoundWave (단계 {depth})");
        go.transform.position = origin;

        var wave = go.AddComponent<SoundWave>();
        wave.Setting = setting;
        wave.Origin = origin;
        wave.Direction = direction;
        wave.Power = power;
        wave.Depth = depth;
        wave.ChainId = chainId;
        // 음수면 설정의 기본 각도
        wave.Angle = angle > 0f ? Mathf.Min(angle, 360f) : setting.angle;
        wave.LengthScale = Mathf.Max(0.01f, lengthScale);
        wave.SpeedScale = Mathf.Max(0.01f, speedScale);
        wave.Source = source;
        wave.CreateVisual();
        return wave;
    }

    private void Update()
    {
        time += Time.deltaTime;
        float progress = Mathf.Clamp01(time * Setting.spreadSpeed * SpeedScale / Length);
        float reach = Length * progress;

        Propagate(reach);
        ShowReach(reach);
        if (progress >= 1f) Destroy(gameObject);
    }

    // reach까지 퍼뜨리며 파면이 처음 지나간 대상에게 반응을 일으킴
    private void Propagate(float reach)
    {
        if (reach <= 0f) return;

        int count = Physics.OverlapSphereNonAlloc(Origin, reach, overlapBuffer, Setting.targetLayers.value, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = overlapBuffer[i];
            if (Source != null && c.transform.IsChildOf(Source)) continue;
            if (!TryGetDistanceInFan(c, reach, out _)) continue;

            ISoundReactive reactive = c.GetComponentInParent<ISoundReactive>();
            if (reactive == null || !reached.Add(reactive)) continue;
            reactive.OnSoundWave(this);
        }
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
        return Angle >= 360f || Vector3.Angle(Direction, to) <= Angle * 0.5f;
    }

    #region 표시

    // 바닥에 눕힌 부채꼴 고리 (도트 그림을 코드로 생성, 퍼진 거리에 따라 프레임 교체)
    private void CreateVisual()
    {
        frames = GetFrames(Setting, Angle);

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
        if (ring == null) return;

        Color color = Setting.waveColor;
        color.a *= value * Mathf.Lerp(0.5f, 1f, Power);
        ring.color = color;
    }

    // 1 유닛 = 반지름 1 (Length로 스케일), 피벗은 부채꼴의 꼭짓점 (원이면 중심)
    private static Sprite[] GetFrames(SoundWaveSetting setting, float waveAngle)
    {
        int radius = setting.pixelRadius;
        int thickness = setting.ringThickness;
        int angle = Mathf.RoundToInt(waveAngle);
        // 사방으로 퍼지면 원 전체를 그리고 피벗을 가운데로 (부채꼴은 위쪽 반원 안에만 그림)
        bool full = angle >= 360;
        byte fill = (byte)Mathf.RoundToInt(setting.fillAlpha * 255f);
        var key = (radius, thickness, angle, fill);
        if (frameCache.TryGetValue(key, out Sprite[] cached) && cached[0] != null) return cached;

        float halfAngle = angle * 0.5f;
        int width = radius * 2 + 1;
        int height = full ? radius * 2 + 1 : radius + 1;
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
                    float py = full ? y - radius : y + 0.5f;
                    float d = Mathf.Sqrt(px * px + py * py);
                    if (d > outer) continue;
                    if (!full && Mathf.Abs(Mathf.Atan2(px, py) * Mathf.Rad2Deg) > halfAngle) continue;

                    // 바깥 고리는 진하게, 안쪽은 지나간 자리로 옅게
                    byte alpha = d >= outer - thickness ? (byte)255 : fill;
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var pivot = new Vector2((radius + 0.5f) / width, full ? (radius + 0.5f) / height : 0f);
            result[f] = Sprite.Create(tex, new Rect(0, 0, width, height), pivot, radius, 0, SpriteMeshType.FullRect);
        }

        frameCache[key] = result;
        return result;
    }

    #endregion

    private void OnDrawGizmos()
    {
        if (Setting == null) return;
        if (Angle >= 360f) DrawCircleGizmo(Origin, Length);
        else DrawFanGizmo(Origin, Direction, Angle, Length);
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
