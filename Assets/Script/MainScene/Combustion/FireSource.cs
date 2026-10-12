using System.Collections.Generic;
using UnityEngine;

// 화염원: 일정 간격으로 맞닿은(CombustionSetting.spreadDistance 안) 대상에게 화염 속성 공격을 줌
// 맞은 쪽이 탈 수 있으면(Combustible) 불이 붙고, 화약통은 도화선에 불이 붙음
// 모닥불은 항상 켜 두고, 불타는 대상은 Combustible이 타는 동안만 켬
// 같은 오브젝트에 IHazardArea가 있으면(용암 호수) 모양 안이나 그 가장자리에 닿은 대상만 맞음
// 트리거 대신 거리 검사를 쓰는 이유: 움직이지 않는 자원·설치물끼리는 트리거 이벤트가 오지 않음
public class FireSource : MonoBehaviour
{
    private const int BufferSize = 256;
    // 모양이 있는 화염원(용암)에 닿았는지 볼 때 대상 주변을 확인하는 간격 (m)
    private const float SampleSpacing = 1.5f;

    [SerializeField] private CombustionSetting setting;
    [Tooltip("닿은 대상에게 주는 직접 피해 (0이면 불만 붙임)")]
    [SerializeField, Min(0)] private int damage;
    [Tooltip("이 화염원의 몸체 (비우면 자식까지 포함한 트리거가 아닌 콜라이더 전부)")]
    [SerializeField] private Collider[] body;

    private static readonly Collider[] buffer = new Collider[BufferSize];
    private readonly HashSet<Health> hitTargets = new();
    private Health self;
    private IHazardArea area;
    private float timer;

    public CombustionSetting Setting => setting;

    private void Awake()
    {
        self = GetComponentInParent<Health>();
        area = GetComponent<IHazardArea>();
        if (body == null || body.Length == 0) body = FindBody();
    }

    private void OnEnable()
    {
        // 켜진 직후 바로 한 번 옮기지 않고 간격만큼 기다림 (불이 붙자마자 주변까지 번지지 않도록)
        timer = setting.spreadInterval;
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer += setting.spreadInterval;

        Emit();
    }

    private void Emit()
    {
        if (!TryGetBounds(out Bounds bounds)) return;

        float reach = setting.spreadDistance;
        // 몸체 상자를 전이 거리만큼 키운 범위에 닿은 대상 (모양이 있으면 대상 몸체 크기까지 여유를 두고 넓게 찾은 뒤 하나씩 확인)
        Vector3 half = bounds.extents + Vector3.one * (area != null ? reach + 5f : reach);
        int count = Physics.OverlapBoxNonAlloc(bounds.center, half, buffer, Quaternion.identity, setting.targetLayers, QueryTriggerInteraction.Ignore);

        hitTargets.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider other = buffer[i];
            Health target = other.GetComponentInParent<Health>();
            if (target == null || target == self || target.IsDead || hitTargets.Contains(target)) continue;
            if (area != null && !Touches(other.bounds, reach)) continue;

            hitTargets.Add(target);
        }

        // 피해로 대상이 쓰러져 풀에 반환되면 콜라이더 목록이 바뀌므로 모은 뒤에 공격
        foreach (Health target in hitTargets)
        {
            if (target != null && !target.IsDead) target.TakeHit(new AttackData { damage = damage, element = ElementType.Fire });
        }
    }

    // 대상 몸체 상자를 reach만큼 키운 바닥 사각형 위의 격자점 중 한 곳이라도 모양 안이면 닿은 것
    // 격자 간격을 용암 칸(1.6m)보다 좁게 둬서 칸을 건너뛰지 않음
    private bool Touches(Bounds target, float reach)
    {
        Vector3 min = target.center - new Vector3(target.extents.x + reach, 0f, target.extents.z + reach);
        float width = (target.extents.x + reach) * 2f;
        float depth = (target.extents.z + reach) * 2f;
        int nx = Mathf.CeilToInt(width / SampleSpacing);
        int nz = Mathf.CeilToInt(depth / SampleSpacing);
        for (int i = 0; i <= nx; i++)
        {
            for (int j = 0; j <= nz; j++)
            {
                if (area.Contains(min + new Vector3(width * i / nx, 0f, depth * j / nz))) return true;
            }
        }
        return false;
    }

    private bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Collider part in body)
        {
            if (part == null || !part.enabled || !part.gameObject.activeInHierarchy) continue;

            if (any) bounds.Encapsulate(part.bounds);
            else bounds = part.bounds;
            any = true;
        }
        return any;
    }

    // 모양이 있는 화염원(용암)은 모양을 감싸는 트리거가 몸체, 나머지는 실제 몸체(트리거가 아닌 콜라이더)
    private Collider[] FindBody()
    {
        var found = new List<Collider>();
        foreach (Collider part in GetComponentsInChildren<Collider>(true))
        {
            if (part.isTrigger == (GetComponent<IHazardArea>() != null)) found.Add(part);
        }
        return found.ToArray();
    }

#if UNITY_EDITOR
    // 선택하면 불이 옮겨붙는 범위를 표시 (모양이 있는 화염원은 몸체 범위만)
    private void OnDrawGizmosSelected()
    {
        if (setting == null) return;
        if (body == null || body.Length == 0) body = FindBody();
        if (!TryGetBounds(out Bounds bounds)) return;

        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.8f);
        float reach = GetComponent<IHazardArea>() != null ? 0f : setting.spreadDistance;
        Gizmos.DrawWireCube(bounds.center, bounds.size + Vector3.one * (reach * 2f));
    }
#endif
}
