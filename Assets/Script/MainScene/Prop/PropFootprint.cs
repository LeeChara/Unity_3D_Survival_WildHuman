using System.Collections.Generic;
using UnityEngine;

// 바닥을 넓게 차지하는 자원이 주변에 다른 자원이 생기지 않게 막는 반경
// 고정 반경은 이 컴포넌트로, 호수처럼 위치마다 크기가 다른 것은 TileLake의 원 배치 + 호수마다의 여유 거리로 구함
// 월드 생성(PropStep, SpawnPointStep)과 재생·지진 배치(PropLifecycle)가 간격 검사에 사용
public class PropFootprint : MonoBehaviour
{
    // 간격 검사 시 주변 청크를 넉넉히 보기 위한 값 (모든 점유 반경은 이 값 이하)
    public const float MaxRadius = 120f;
    // 호수 가장자리 여유는 호수마다 다름 (TileLake.FootprintMargin)

    [Tooltip("이 반경 안에는 다른 자원이 생기지 않음")]
    [SerializeField, Range(0f, 10f)] private float radius = 3f;

    // 프리팹마다 컴포넌트를 찾는 비용을 줄이기 위한 캐시 (자원 수천 개를 반복 검사함)
    private static readonly Dictionary<GameObject, (TileLake lake, float radius)> cache = new();

    public static float RadiusOf(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return 0f;

        if (!cache.TryGetValue(prefab, out var info))
        {
            prefab.TryGetComponent(out TileLake lake);
            float fixedRadius = prefab.TryGetComponent(out PropFootprint footprint) ? footprint.radius : 0f;
            info = (lake, fixedRadius);
            cache[prefab] = info;
        }

        return info.lake != null ? info.lake.LayoutRadius(position) + info.lake.FootprintMargin : info.radius;
    }
}
