using System.Collections.Generic;
using UnityEngine;

// 용암 호수: 칸 지도 호수(TileLake) + 호수 안에 나눠 둔 광원
// 피해·불 번짐은 같은 오브젝트의 FireHazard·FireSource가 이 호수의 칸 지도(IHazardArea)로 판정
// 자원 여유 거리(footprintMargin)는 넉넉히 6m: 용암은 화염원이라 자연히 생긴 자원(유황 등)이 처음부터 불이 옮겨붙는 거리에 있지 않도록
// (가장 큰 자원 몸체 약 3.5m + CombustionSetting.objectSpreadDistance 1m + 타일 모서리 약 1.1m. 전이 거리를 늘리면 같이 늘릴 것)
public class LavaLake : TileLake
{
    [Header("빛")]
    [Tooltip("호수 안에 나눠 두는 광원 최대 개수")]
    [SerializeField, Range(1, 8)] private int maxLights = 6;
    [Tooltip("광원 사이 최소 간격 (m)")]
    [SerializeField] private float lightSpacing = 20f;
    [Tooltip("광원 하나의 반경 (광원이 하나뿐이면 호수 반경 + lightPadding)")]
    [SerializeField] private float lightRadius = 14f;
    [SerializeField] private float lightPadding = 3f;
    [SerializeField] private float lightIntensity = 1.2f;
    [SerializeField] private Color lightColor = new Color(1f, 0.5f, 0.2f);
    [SerializeField, Range(0f, 0.5f)] private float lightFlicker = 0.2f;

    private LightSource[] lights;

    private void Awake()
    {
        lights = new LightSource[maxLights];
        for (int i = 0; i < maxLights; i++)
        {
            var child = new GameObject("Glow" + i);
            child.transform.SetParent(transform, false);
            lights[i] = child.AddComponent<LightSource>();
            child.SetActive(false);
        }
    }

    protected override object ComputeExtra(Shape shape) => PickLightSpots(shape);

    protected override void OnBuilt()
    {
        Shape shape = Current;
        var spots = (List<Vector3>)shape.extra;
        for (int i = 0; i < lights.Length; i++)
        {
            bool used = i < spots.Count;
            lights[i].gameObject.SetActive(used);
            if (!used) continue;

            lights[i].transform.localPosition = spots[i];
            float radius = spots.Count == 1 ? shape.radius + lightPadding : lightRadius;
            lights[i].Configure(radius, lightIntensity, lightColor, lightFlicker);
        }
    }

    // 호수 안에서 서로 lightSpacing 이상 떨어진 지점을, 가운데부터 가장 먼 곳 순으로 고름
    private List<Vector3> PickLightSpots(Shape shape)
    {
        float cell = CellSize;
        var candidates = new List<Vector3>();
        int stride = Mathf.Max(1, Mathf.RoundToInt(lightSpacing * 0.25f / cell));
        for (int y = 0; y < shape.cells; y += stride)
        {
            for (int x = 0; x < shape.cells; x += stride)
            {
                if (shape.inside[y * shape.cells + x]) candidates.Add(new Vector3((x + 0.5f) * cell - shape.half, 0.5f, (y + 0.5f) * cell - shape.half));
            }
        }

        var spots = new List<Vector3> { new Vector3(0f, 0.5f, 0f) };
        while (spots.Count < maxLights)
        {
            Vector3 best = default;
            float bestDistance = lightSpacing;
            foreach (Vector3 candidate in candidates)
            {
                float nearest = float.MaxValue;
                foreach (Vector3 spot in spots) nearest = Mathf.Min(nearest, (candidate - spot).sqrMagnitude);
                nearest = Mathf.Sqrt(nearest);
                if (nearest > bestDistance)
                {
                    bestDistance = nearest;
                    best = candidate;
                }
            }
            if (bestDistance <= lightSpacing) break;
            spots.Add(best);
        }
        return spots;
    }
}
