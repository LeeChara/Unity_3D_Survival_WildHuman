using System.Collections.Generic;
using UnityEngine;

// 눈밭(설원)에 남는 발자국을 관리 (씬에 하나)
// 생물마다 붙은 Footprinter가 걸음마다 Stamp를 부르면, 바이옴·지형을 확인하고 바닥에 눕힌 그림을 찍음
// 발자국은 일정 시간 뒤 서서히 사라짐. 저장하지 않음
// 개수 상한을 넘으면 가장 오래된 것부터 다시 씀 (몬스터가 많아도 그리는 양이 늘지 않음)
// 그림자와 같은 Shadow 정렬 레이어의 더 앞 순서로 그려서 그림자·호수·캐릭터보다 항상 아래에 깔림
public class FootprintSystem : MonoBehaviour
{
    private const int SortingOrder = -10;

    public static FootprintSystem Instance { get; private set; }

    [SerializeField] private WorldState worldState;
    [Tooltip("발자국이 남는 바이옴")]
    [SerializeField] private BiomeData[] biomes;
    [Tooltip("발자국이 남지 않는 지형 레이어 (물·빙판·용암 호수)")]
    [SerializeField] private LayerMask blockLayers;

    [Header("표시")]
    [Tooltip("발자국 색 (그림은 흰색, 알파가 진하기)")]
    [SerializeField] private Color color = new Color(0.55f, 0.63f, 0.78f, 0.55f);
    [Tooltip("그대로 남아 있는 시간 (초)")]
    [SerializeField, Min(0f)] private float lifetime = 20f;
    [Tooltip("사라지는 데 걸리는 시간 (초)")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 6f;
    [Tooltip("동시에 남아 있을 수 있는 최대 개수")]
    [SerializeField, Min(1)] private int maxCount = 600;
    [Tooltip("바닥에서 띄우는 높이 (m)")]
    [SerializeField, Min(0f)] private float lift = 0.02f;

    private struct Print
    {
        public SpriteRenderer renderer;
        public float bornAt;
    }

    private readonly List<Print> prints = new();
    // 다음에 다시 쓸 칸 (상한을 채운 뒤에는 가장 오래된 것)
    private int next;
    private int activeCount;
    private Transform container;

    private void Awake()
    {
        Instance = this;
        container = new GameObject("Footprints").transform;
        container.SetParent(transform, false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 발자국을 남길 수 있는 자리인지 (바이옴·지형)
    public bool CanStamp(Vector3 position)
    {
        if (worldState == null || biomes == null || biomes.Length == 0) return false;

        BiomeData biome = worldState.GetBiome(worldState.WorldToChunk(position));
        if (biome == null || System.Array.IndexOf(biomes, biome) < 0) return false;
        return !HazardQuery.IsInside(position, blockLayers);
    }

    // yaw: 발자국 위쪽이 향할 방향 (도, 월드 Y축 기준)
    public void Stamp(Vector3 position, float yaw, Sprite sprite, float scale, bool mirror)
    {
        if (sprite == null || !CanStamp(position)) return;

        Print print = Take();
        SpriteRenderer r = print.renderer;
        r.sprite = sprite;
        r.color = color;
        r.flipX = mirror;
        r.transform.SetPositionAndRotation(new Vector3(position.x, lift, position.z), Quaternion.Euler(90f, yaw, 0f));
        r.transform.localScale = new Vector3(scale, scale, 1f);
        r.gameObject.SetActive(true);
        print.bornAt = Time.time;
        prints[next] = print;
        next = (next + 1) % maxCount;
    }

    private Print Take()
    {
        if (prints.Count < maxCount && next == prints.Count)
        {
            var go = new GameObject("Footprint");
            go.transform.SetParent(container, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingLayerName = ShadowSetting.SortingLayerName;
            r.sortingOrder = SortingOrder;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            prints.Add(new Print { renderer = r });
            activeCount++;
            return prints[next];
        }

        Print reused = prints[next];
        if (!reused.renderer.gameObject.activeSelf) activeCount++;
        return reused;
    }

    private void Update()
    {
        if (activeCount == 0) return;

        float now = Time.time;
        for (int i = 0; i < prints.Count; i++)
        {
            Print print = prints[i];
            if (!print.renderer.gameObject.activeSelf) continue;

            float age = now - print.bornAt - lifetime;
            if (age <= 0f) continue;

            if (age >= fadeDuration)
            {
                print.renderer.gameObject.SetActive(false);
                activeCount--;
                continue;
            }

            Color c = color;
            c.a *= 1f - age / fadeDuration;
            print.renderer.color = c;
        }
    }
}
