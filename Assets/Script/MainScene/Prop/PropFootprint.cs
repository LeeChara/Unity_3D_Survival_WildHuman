using System.Collections.Generic;
using UnityEngine;

// 바닥을 넓게 차지하는 자원이 주변에 다른 자원이 생기지 않게 막는 반경
// 고정 반경은 이 컴포넌트로, 용암 호수처럼 위치마다 크기가 다른 것은 LavaLake의 원 배치로 구함
// 월드 생성(PropStep, SpawnPointStep)과 재생·지진 배치(PropLifecycle)가 간격 검사에 사용
public class PropFootprint : MonoBehaviour
{
    // 간격 검사 시 주변 청크를 넉넉히 보기 위한 값 (모든 점유 반경은 이 값 이하)
    public const float MaxRadius = 120f;
    // 용암 호수 가장자리에 자원이 바로 붙지 않도록 더하는 여유
    // 용암은 화염원이라, 자연히 생긴 자원(유황 등)이 처음부터 불이 옮겨붙는 거리에 있지 않도록 넉넉히 둠
    // (가장 큰 자원 몸체 약 3.5m + CombustionSetting.spreadDistance 1m + 타일 모서리 약 1.1m. 전이 거리를 늘리면 같이 늘릴 것)
    private const float LakeMargin = 6f;

    [Tooltip("이 반경 안에는 다른 자원이 생기지 않음")]
    [SerializeField, Range(0f, 10f)] private float radius = 3f;

    // 프리팹마다 컴포넌트를 찾는 비용을 줄이기 위한 캐시 (자원 수천 개를 반복 검사함)
    private static readonly Dictionary<GameObject, (LavaLake lake, float radius)> cache = new();

    public static float RadiusOf(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return 0f;

        if (!cache.TryGetValue(prefab, out var info))
        {
            prefab.TryGetComponent(out LavaLake lake);
            float fixedRadius = prefab.TryGetComponent(out PropFootprint footprint) ? footprint.radius : 0f;
            info = (lake, fixedRadius);
            cache[prefab] = info;
        }

        return info.lake != null ? info.lake.LayoutRadius(position) + LakeMargin : info.radius;
    }
}
