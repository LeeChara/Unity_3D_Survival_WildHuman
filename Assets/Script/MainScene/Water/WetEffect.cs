using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 젖음 이펙트 (프리팹 없이 코드로 만들고 WaterSetting의 스프라이트를 씀)
// 젖은 동안 대상 그림 범위 안에서 물방울이 떨어짐 (Wettable이 대상의 자식으로 한 번 만들고 켜고 끔)
// 마를 때 피어오르는 김은 파티클(WaterSetting.steamEffect)로 재생
// 불타는 이펙트(BurnEffect)와 같은 방식으로 화면 가로(카메라 오른쪽, 수평 45도) 평면에 그림
public class WetEffect : MonoBehaviour
{
    // 카메라 각도는 프로젝트 전체에서 45도로 고정 (BurnEffect와 같은 전제)
    private const float CameraYAngle = 45f;

    // 물방울
    private const float DropsPerMeter = 2.5f;
    private const int MaxDrops = 6;
    private const float FallSpeed = 1.1f;
    private const float FallDistance = 0.55f;
    private const float MaxRespawnDelay = 0.8f;
    private static readonly Color DropColor = new Color(0.55f, 0.78f, 1f, 1f);

    private class Particle
    {
        public SpriteRenderer renderer;
        public Vector2 start;
        public float age;
        public float life;
        public float delay;
    }

    // 물방울을 보여 줄 화면 중심(카메라가 비추는 지면 지점)으로부터의 거리 (화면 약 32x26m보다 조금 넉넉히)
    private const float ViewRange = 28f;
    private static int viewFrame = -1;
    private static Vector3 viewCenter;

    private readonly List<Particle> particles = new();
    private SortingGroup sortingGroup;
    private Sprite sprite;
    private Color baseColor;
    // 덮는 범위 (화면 가로 너비, 높이)
    private float width;
    private float height;

    // 화면 근처인지 (화면 중심은 프레임마다 한 번만 계산)
    public static bool IsNearView(Vector3 position)
    {
        if (viewFrame != Time.frameCount)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            viewFrame = Time.frameCount;

            Transform t = cam.transform;
            Vector3 dir = t.forward;
            viewCenter = dir.y < -0.01f ? t.position + dir * (-t.position.y / dir.y) : t.position;
        }

        Vector3 offset = position - viewCenter;
        offset.y = 0f;
        return offset.sqrMagnitude <= ViewRange * ViewRange;
    }

    // 대상의 자식으로 붙어 물방울을 떨어뜨리는 지속형 이펙트를 만듦 (처음에는 꺼진 상태)
    public static WetEffect Attach(WaterSetting setting, Transform target)
    {
        WetEffect effect = Create("WetEffect", setting.dropSprite, DropColor, target);
        effect.gameObject.SetActive(false);
        return effect;
    }

    private static WetEffect Create(string name, Sprite sprite, Color color, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        WetEffect effect = go.AddComponent<WetEffect>();
        effect.sortingGroup = go.AddComponent<SortingGroup>();
        effect.sprite = sprite;
        effect.baseColor = color;
        return effect;
    }

    // 켤 때마다 대상 그림 크기를 다시 재서 맞춤
    public void Show(Transform target)
    {
        gameObject.SetActive(true);
        if (sprite == null || !BurnEffect.TryMeasure(target, transform, null, out Bounds cover)) return;

        Fit(cover, target);
        int count = Mathf.Clamp(Mathf.RoundToInt(width * DropsPerMeter), 1, MaxDrops);
        while (particles.Count < count) AddParticle();
        for (int i = 0; i < particles.Count; i++)
        {
            Particle drop = particles[i];
            drop.renderer.gameObject.SetActive(i < count);
            // 처음에는 흩어서 떨어지도록 대기 시간을 나눠 줌
            SpawnDrop(drop, Random.value * MaxRespawnDelay);
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        foreach (Particle p in particles)
        {
            if (!p.renderer.gameObject.activeSelf) continue;

            if (p.delay > 0f)
            {
                p.delay -= dt;
                p.renderer.enabled = p.delay <= 0f;
                continue;
            }

            p.age += dt;
            float t = Mathf.Clamp01(p.age / p.life);

            float y = p.start.y - FallSpeed * p.age;
            p.renderer.transform.localPosition = new Vector3(p.start.x, y, 0f);
            // 끝으로 갈수록 흐려짐
            SetAlpha(p, t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f);
            if (t >= 1f || y <= 0f) SpawnDrop(p, Random.value * MaxRespawnDelay);
        }
    }

    private Particle AddParticle()
    {
        var go = new GameObject("Particle");
        go.transform.SetParent(transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = baseColor;
        var particle = new Particle { renderer = renderer };
        particles.Add(particle);
        return particle;
    }

    // 그림 위쪽 어딘가에서 떨어지기 시작
    private void SpawnDrop(Particle drop, float delay)
    {
        drop.start = new Vector2(Random.Range(-0.4f, 0.4f) * width, Random.Range(0.4f, 0.9f) * height);
        drop.age = 0f;
        drop.life = FallDistance / FallSpeed;
        drop.delay = delay;
        drop.renderer.enabled = delay <= 0f;
        drop.renderer.transform.localPosition = drop.start;
        SetAlpha(drop, 1f);
    }

    private void SetAlpha(Particle p, float alpha)
    {
        p.renderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * Mathf.Clamp01(alpha));
    }

    private void Fit(Bounds cover, Transform target)
    {
        // 화면 가로 방향으로 본 너비 (월드 축 상자를 카메라 오른쪽 축에 투영한 길이)
        width = (cover.size.x + cover.size.z) * Mathf.Sin(CameraYAngle * Mathf.Deg2Rad);
        height = Mathf.Max(cover.size.y, 0.3f);

        transform.SetPositionAndRotation(new Vector3(cover.center.x, cover.min.y, cover.center.z), Quaternion.Euler(0f, CameraYAngle, 0f));
        transform.localScale = Vector3.one;
        if (transform.parent != null)
        {
            // 부모 배율을 상쇄해서 월드 기준 크기로 맞춤
            Vector3 parentScale = transform.parent.lossyScale;
            transform.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
        }

        BurnEffect.SortOver(sortingGroup, target, cover);
    }
}
