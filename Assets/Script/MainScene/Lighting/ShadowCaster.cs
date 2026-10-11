using System.Collections.Generic;
using UnityEngine;

// 발밑에 타원 그림자를 깔고 해 방향으로 늘림 (값은 ShadowSystem이 매 프레임 넣어 줌)
// 밤에는 가까운 광원(모닥불, 랜턴 등)의 반대쪽으로도 그림자를 드리움 (가장 강한 광원 몇 개만)
// 그림자 오브젝트는 자식이 아닌 별도 컨테이너에 둬서 SortingGroup·HitFlash 등 파츠 처리에 끼지 않게 함
public class ShadowCaster : MonoBehaviour
{
    // 모든 그림자가 함께 쓰는 1×1 흰 사각형 (타원 모양은 셰이더가 UV로 그림)
    private static Sprite quadSprite;
    private static Transform container;

    // 이보다 약한 광원 그림자는 거의 보이지 않으므로 만들지 않음
    private const float MinLightStrength = 0.02f;

    // 광원 후보를 고를 때 쓰는 임시 버퍼 (매번 할당하지 않도록 공유)
    private static readonly int[] pickedLights = new int[ShadowSetting.MaxLightShadows];
    private static readonly float[] pickedStrengths = new float[ShadowSetting.MaxLightShadows];
    private static readonly float[] pickedDistances = new float[ShadowSetting.MaxLightShadows];

    [SerializeField] private ShadowSetting setting;
    [Tooltip("그림자 폭 (월드 단위)")]
    [SerializeField, Min(0.01f)] private float width = 0.8f;
    [Tooltip("그림자를 드리우는 높이 (클수록 해가 낮을 때 길게 뻗음)")]
    [SerializeField, Min(0f)] private float height = 1.5f;
    [Tooltip("발밑 위치 보정 (피벗이 발밑이 아닌 경우, 오브젝트 회전 기준). 그림자는 이 높이에 깔려서 화면에서 발에 붙어 보임")]
    [SerializeField] private Vector3 footOffset;
    [Tooltip("움직이지 않는 오브젝트 (자원·설치물). 해가 조금씩 바뀔 때마다가 아니라 일정 단계마다, 또는 옮겨졌을 때만 다시 계산")]
    [SerializeField] private bool isStatic;

    private SpriteRenderer shadow;
    private readonly SpriteRenderer[] lightShadows = new SpriteRenderer[ShadowSetting.MaxLightShadows];
    // 지금 켜져 있는 광원 그림자 수
    private int litCount;
    // 마지막으로 계산한 발밑 위치 (광원 그림자에서 매 프레임 Transform을 다시 읽지 않도록)
    private Vector3 cachedFoot;

    // 크기(scale)는 빼고 회전만 적용 (자원 프리팹은 scale이 10이라 보정값이 함께 커지지 않게)
    private Vector3 Foot => transform.position + transform.rotation * footOffset;

    private void OnEnable()
    {
        if (shadow == null) shadow = CreateRenderer("Shadow", setting.material, 0);
        shadow.gameObject.SetActive(true);
        ShadowSystem.Register(this);
        UpdateShadow();
    }

    private void OnDisable()
    {
        ShadowSystem.Unregister(this);
        if (shadow != null) shadow.gameObject.SetActive(false);
        HideLightShadows(0);
    }

    private void OnDestroy()
    {
        if (shadow != null) Destroy(shadow.gameObject);
        foreach (SpriteRenderer r in lightShadows)
        {
            if (r != null) Destroy(r.gameObject);
        }
    }

    // 정지 오브젝트는 해가 한 단계 바뀌었거나 위치가 바뀐 경우만 (풀에서 꺼내 옮긴 자원 등)
    public void Refresh(bool sunStepped)
    {
        if (isStatic && !sunStepped && !transform.hasChanged) return;
        UpdateShadow();
    }

    // 발밑에서 해 반대쪽으로 뻗는 타원: 해가 높으면 지름 width의 원, 낮으면 길쭉해짐
    public void UpdateShadow()
    {
        cachedFoot = Foot;
        float length = Mathf.Max(width, height * ShadowSystem.SunLength);
        Place(shadow, cachedFoot, ShadowSystem.SunDirection, length);
        transform.hasChanged = false;
    }

    // 빛 반경 안의 광원 중 가장 강한 몇 개의 반대쪽으로 그림자를 드리움
    // 광원에 가까울수록 짧고 진하며, 반경 끝으로 갈수록 길고 옅어짐. darkness가 0(낮)이면 모두 끔
    // 광원은 깜빡이거나 움직이므로 정지 오브젝트도 빛 안에 있는 동안은 매 프레임 갱신
    // lights는 ShadowSystem이 프레임마다 한 번 읽어 둔 값 (오브젝트마다 Transform·깜빡임을 다시 계산하지 않도록)
    public void RefreshLights(List<ShadowSystem.ShadowLight> lights, float darkness)
    {
        if (darkness <= 0f || lights.Count == 0)
        {
            HideLightShadows(0);
            return;
        }

        Vector3 foot = cachedFoot;
        int count = PickLights(lights, foot);

        for (int i = 0; i < count; i++)
        {
            ShadowSystem.ShadowLight light = lights[pickedLights[i]];
            float distance = pickedDistances[i];

            Vector3 dir = foot - light.position;
            dir.y = 0f;
            dir /= distance;

            float length = Mathf.Max(width, height * Mathf.Lerp(setting.nearLightLength, setting.farLightLength, distance / light.radius));

            SpriteRenderer r = lightShadows[i];
            if (r == null) r = lightShadows[i] = CreateRenderer($"LightShadow{i}", setting.lightMaterial, -1);
            if (!r.enabled) r.enabled = true;

            Place(r, foot, dir, length);
            r.color = new Color(0f, 0f, 0f, setting.lightAlpha * pickedStrengths[i] * darkness);
        }

        HideLightShadows(count);
        litCount = count;
    }

    // 강한 순으로 최대 MaxLightShadows개를 고름 (자기 자신에게 달린 광원은 제외: 랜턴을 든 플레이어, 모닥불 자체 등)
    private int PickLights(List<ShadowSystem.ShadowLight> lights, Vector3 foot)
    {
        int count = 0;
        for (int li = 0; li < lights.Count; li++)
        {
            ShadowSystem.ShadowLight light = lights[li];
            float dx = foot.x - light.position.x;
            float dz = foot.z - light.position.z;
            float sqr = dx * dx + dz * dz;
            if (sqr >= light.radius * light.radius) continue;

            float distance = Mathf.Sqrt(sqr);
            // 광원과 겹친 자리는 방향을 정할 수 없음
            if (distance < 0.01f) continue;

            // 빛 반경 대부분에서는 진하게 유지되다가 가장자리에서만 옅어짐
            float t = distance / light.radius;
            float strength = (1f - t * t) * light.intensity;
            if (strength < MinLightStrength) continue;
            if (count == pickedLights.Length && strength <= pickedStrengths[count - 1]) continue;
            if (light.source.transform.IsChildOf(transform)) continue;

            // 강한 순으로 끼워 넣기 (후보가 2개뿐이라 단순 삽입)
            int index = count < pickedLights.Length ? count++ : count - 1;
            while (index > 0 && pickedStrengths[index - 1] < strength)
            {
                pickedLights[index] = pickedLights[index - 1];
                pickedStrengths[index] = pickedStrengths[index - 1];
                pickedDistances[index] = pickedDistances[index - 1];
                index--;
            }
            pickedLights[index] = li;
            pickedStrengths[index] = strength;
            pickedDistances[index] = distance;
        }
        return count;
    }

    // from번째부터 켜져 있던 광원 그림자를 끔
    private void HideLightShadows(int from)
    {
        for (int i = from; i < litCount; i++)
        {
            if (lightShadows[i] != null) lightShadows[i].enabled = false;
        }
        litCount = Mathf.Min(litCount, from);
    }

    // 발밑에서 dir 방향으로 뻗는 타원 (가까운 끝이 발밑 뒤로 width의 절반만큼 걸침)
    private void Place(SpriteRenderer r, Vector3 foot, Vector3 dir, float length)
    {
        foot.y += setting.lift;

        Transform t = r.transform;
        t.position = foot + dir * ((length - width) * 0.5f);
        // 바닥에 눕힌 뒤(X 90도) 스프라이트의 위쪽(Y)이 dir을 향하도록 회전
        t.rotation = Quaternion.Euler(90f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
        t.localScale = new Vector3(width, length, 1f);
    }

    // 광원 그림자는 해 그림자보다 먼저 그려서(order -1), 겹친 곳은 광원 그림자가 스텐실을 차지하게 함
    private SpriteRenderer CreateRenderer(string label, Material material, int order)
    {
        if (quadSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            quadSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                tex.width, 0, SpriteMeshType.FullRect);
        }
        // 씬이 바뀌면 컨테이너도 함께 사라지므로 다시 만듦
        if (container == null) container = new GameObject("Shadows").transform;

        var go = new GameObject($"{label} ({name})");
        go.transform.SetParent(container, false);

        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = quadSprite;
        r.sharedMaterial = material;
        // 해 그림자의 진하기는 셰이더 전역값(_ShadowSunAlpha)이 곱해짐
        r.color = Color.black;
        r.sortingLayerName = ShadowSetting.SortingLayerName;
        r.sortingOrder = order;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 foot = Foot;
        Gizmos.color = new Color(0f, 0f, 0f, 0.6f);
        Gizmos.DrawWireCube(foot, new Vector3(width, 0.01f, width));
        Gizmos.DrawLine(foot, foot + Vector3.up * height);
    }
}
