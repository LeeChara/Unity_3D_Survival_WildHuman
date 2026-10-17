using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 스프라이트 파티클 재생기 (EffectData를 한 번 뿌리고 끝나면 스스로 정리)
// 불타는 이펙트(BurnEffect)·젖음 이펙트(WetEffect)와 같은 방식으로 화면 가로(카메라 오른쪽, 수평 45도) 평면에 그림
// 첫 재생 때 씬에 하나 만들어지고, 그림(SpriteRenderer)과 묶음은 풀에서 재사용
// 설정의 '파티클'(GameOptions.ParticleLevel)이 끄기면 재생하지 않고, 적게면 수를 절반으로 줄임
public class Effects : MonoBehaviour
{
    // 카메라 각도는 프로젝트 전체에서 45도로 고정 (BurnEffect와 같은 전제)
    private const float CameraYAngle = 45f;
    // 한 번에 살아 있을 수 있는 파티클 수 (넘으면 새로 뿌리는 것을 줄임)
    private const int MaxParticles = 400;

    // 계산은 EffectParticle, 여기서는 그릴 SpriteRenderer만 더 가짐
    private class Particle : EffectParticle
    {
        public SpriteRenderer renderer;
    }

    // 한 번 재생한 이펙트의 파티클 묶음 (대상 발밑을 원점으로 둔 화면 평면)
    private class Group
    {
        public Transform root;
        public SortingGroup sortingGroup;
        public readonly List<Particle> particles = new();
        // 대상 없이 재생했으면 매 프레임 위치로 앞뒤를 다시 계산 (화면 기준점이 바뀌어도 맞도록)
        public bool sortByDepth;
        // 따라갈 대상 (없으면 재생한 자리에 머묾)과 대상 기준 위치
        public Transform follow;
        public Vector3 followOffset;
    }

    private static Effects instance;

    private readonly List<Group> activeGroups = new();
    private readonly Stack<Group> groupPool = new();
    private readonly Stack<Particle> particlePool = new();
    private int aliveCount;

    // 한 점에서 재생 (예: 지진으로 솟는 자리)
    public static void Play(EffectData data, Vector3 position, Color? tint = null)
    {
        Play(data, new Bounds(position, Vector3.zero), null, tint);
    }

    // 대상 그림을 덮는 범위에서 재생하고 대상보다 앞에 그림 (예: 자원을 때림)
    // follow면 파티클 묶음이 대상을 따라 움직임 (예: 먹을 때 몸 주변 반짝이)
    public static void PlayOn(EffectData data, Transform target, Color? tint = null, bool follow = false)
    {
        if (data == null || target == null) return;

        if (!BurnEffect.TryMeasure(target, null, null, out Bounds cover)) cover = new Bounds(target.position, Vector3.zero);
        Play(data, cover, target, tint, follow);
    }

    // cover: 파티클이 퍼질 기준 범위 (월드), target: 앞뒤를 맞출 대상 (없으면 위치로 계산)
    public static void Play(EffectData data, Bounds cover, Transform target, Color? tint = null, bool follow = false)
    {
        if (data == null || data.emitters == null || GameOptions.ParticleLevel <= 0) return;
        if (!WetEffect.IsNearView(cover.center)) return;

        if (instance == null) instance = new GameObject("Effects").AddComponent<Effects>();
        instance.Spawn(data, cover, target, tint ?? Color.white, follow);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Spawn(EffectData data, Bounds cover, Transform target, Color tint, bool follow)
    {
        Group group = GetGroup();
        group.root.SetPositionAndRotation(new Vector3(cover.center.x, cover.min.y, cover.center.z), Quaternion.Euler(0f, CameraYAngle, 0f));
        group.sortByDepth = target == null;
        group.follow = follow ? target : null;
        if (group.follow != null) group.followOffset = group.root.position - target.position;
        if (target != null) BurnEffect.SortOver(group.sortingGroup, target, cover);
        else UpdateDepth(group);

        // 화면 가로 방향으로 본 너비 (월드 축 상자를 카메라 오른쪽 축에 투영한 길이)
        float width = (cover.size.x + cover.size.z) * Mathf.Sin(CameraYAngle * Mathf.Deg2Rad);
        float height = cover.size.y;
        float countScale = GameOptions.ParticleLevel == 1 ? 0.5f : 1f;

        foreach (ParticleEmitter emitter in data.emitters)
        {
            if (emitter.frames == null || emitter.frames.Length == 0) continue;

            int count = Mathf.Min(EffectParticle.RollCount(emitter, countScale), MaxParticles - aliveCount);
            for (int i = 0; i < count; i++)
            {
                Particle p = GetParticle(group);
                p.Init(emitter, width, height, tint);
                p.renderer.enabled = !p.Waiting;
                Apply(p);
            }
        }

        if (group.particles.Count == 0) ReleaseGroup(group);
        else activeGroups.Add(group);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        for (int g = activeGroups.Count - 1; g >= 0; g--)
        {
            Group group = activeGroups[g];
            if (group.follow != null)
            {
                // 따라가는 대상은 움직이며 앞뒤 순서가 바뀌므로 매 프레임 다시 맞춤
                group.root.position = group.follow.position + group.followOffset;
                BurnEffect.SortOver(group.sortingGroup, group.follow, new Bounds(group.root.position, Vector3.zero));
            }
            if (group.sortByDepth) UpdateDepth(group);

            for (int i = group.particles.Count - 1; i >= 0; i--)
            {
                Particle p = group.particles[i];
                p.Tick(dt);
                if (p.Waiting) continue;
                if (p.Finished)
                {
                    ReleaseParticle(group, i);
                    continue;
                }

                p.renderer.enabled = true;
                Apply(p);
            }

            if (group.particles.Count == 0)
            {
                activeGroups.RemoveAt(g);
                ReleaseGroup(group);
            }
        }
    }

    private static void Apply(Particle p)
    {
        p.renderer.sprite = p.CurrentSprite;
        Vector2 position = p.DrawPosition;
        p.renderer.transform.localPosition = new Vector3(position.x, position.y, 0f);
        float scale = p.DrawScale;
        p.renderer.transform.localScale = new Vector3(scale, scale, 1f);
        p.renderer.color = p.DrawColor;
    }

    private static void UpdateDepth(Group group)
    {
        if (DepthSortManager.Instance == null) return;

        group.sortingGroup.sortingOrder = DepthSortManager.Instance.GetSortingOrder(group.root.position) + 1;
    }

    #region 풀

    private Group GetGroup()
    {
        if (groupPool.Count > 0)
        {
            Group pooled = groupPool.Pop();
            pooled.root.gameObject.SetActive(true);
            return pooled;
        }

        var go = new GameObject("EffectGroup");
        go.transform.SetParent(transform, false);
        return new Group { root = go.transform, sortingGroup = go.AddComponent<SortingGroup>() };
    }

    private void ReleaseGroup(Group group)
    {
        group.follow = null;
        group.root.gameObject.SetActive(false);
        groupPool.Push(group);
    }

    private Particle GetParticle(Group group)
    {
        Particle p;
        if (particlePool.Count > 0)
        {
            p = particlePool.Pop();
            p.renderer.gameObject.SetActive(true);
        }
        else
        {
            var go = new GameObject("Particle");
            p = new Particle { renderer = go.AddComponent<SpriteRenderer>() };
        }

        p.renderer.transform.SetParent(group.root, false);
        p.renderer.transform.localRotation = Quaternion.identity;
        group.particles.Add(p);
        aliveCount++;
        return p;
    }

    private void ReleaseParticle(Group group, int index)
    {
        Particle p = group.particles[index];
        group.particles.RemoveAt(index);
        p.renderer.gameObject.SetActive(false);
        p.renderer.transform.SetParent(transform, false);
        particlePool.Push(p);
        aliveCount--;
    }

    #endregion
}
