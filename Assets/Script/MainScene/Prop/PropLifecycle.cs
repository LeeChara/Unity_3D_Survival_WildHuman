using UnityEngine;

// 자원의 재생 규칙(PropData의 growInto·leaveBehind·regrowAs)을 월드 시간에 맞춰 적용
// - 파괴: leaveBehind를 그 자리에 남기고(다 타서 사라진 경우 제외), regrowAs는 같은 청크의 재생 목록에 예약
// - 청크가 로드될 때와 checkInterval마다 로드된 청크에서 시간이 된 변화·재생을 처리
//   (언로드된 청크는 다음에 로드될 때 밀린 만큼 한꺼번에 처리)
// - 눈앞에서 갑자기 바뀌거나 생기지 않도록 화면에 보이는 자리는 다음 검사로 미룸
public class PropLifecycle : MonoBehaviour
{
    [SerializeField] private PropSpawner spawner;
    [SerializeField] private WorldState worldState;
    [SerializeField] private DayNightCycle clock;
    [Tooltip("로드된 청크에서 시간이 된 변화·재생을 확인하는 간격(초)")]
    [SerializeField, Min(0.5f)] private float checkInterval = 10f;

    [Header("새 위치 찾기")]
    [Tooltip("재생할 자리를 찾기 위한 검사당 최대 시도 횟수 (못 찾으면 다음 검사에서 다시 시도)")]
    [SerializeField, Min(1)] private int placementAttempts = 15;
    [Tooltip("이 레이어의 물체(설치물)에서 structureClearance 안에는 생기지 않음")]
    [SerializeField] private LayerMask structureLayers;
    [SerializeField, Min(0f)] private float structureClearance = 3f;
    [Tooltip("이 레이어의 물체(플레이어, 몬스터)와 blockingRadius 안에서 겹치는 자리에는 생기지 않음")]
    [SerializeField] private LayerMask blockingLayers;
    [SerializeField, Min(0f)] private float blockingRadius = 1f;

    [Header("화면 밖 판정")]
    [Tooltip("자원의 대략적인 높이 (발밑과 꼭대기 중 하나라도 화면 안이면 보이는 것으로 봄)")]
    [SerializeField, Min(0f)] private float propHeight = 6f;
    [Tooltip("화면 가장자리 바깥으로 더 볼 여유 (뷰포트 비율)")]
    [SerializeField, Min(0f)] private float viewMargin = 0.1f;

    private float checkTimer;

    public float Now => clock.WorldTime;

    private void OnEnable()
    {
        spawner.PropDepleted += OnPropDepleted;
        spawner.ChunkPropsSpawned += Process;
    }

    private void OnDisable()
    {
        spawner.PropDepleted -= OnPropDepleted;
        spawner.ChunkPropsSpawned -= Process;
    }

    private void Update()
    {
        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;
        checkTimer = checkInterval;

        // Process는 청크 안의 목록만 바꾸고 로드된 청크 집합은 바꾸지 않음
        foreach (Vector2Int coord in spawner.LoadedChunks)
        {
            Process(coord);
        }
    }

    public static PropData DataOf(GameObject prefab)
    {
        return prefab != null && prefab.TryGetComponent(out PropHealth health) ? health.Data : null;
    }

    // 자원을 추가하고, 시간이 지나면 바뀌는 종류면 바뀔 시간을 예약
    public void Place(Vector2Int coord, GameObject prefab, Vector3 position)
    {
        int index = spawner.AddProp(coord, prefab, position);
        ScheduleGrowth(coord, index, prefab);
    }

    private void ScheduleGrowth(Vector2Int coord, int index, GameObject prefab)
    {
        PropData data = DataOf(prefab);
        // 0 이하는 "바뀌지 않음"이므로 시작 직후(월드 시간 0)에도 예약되도록 아주 작은 값을 보장
        float time = data != null && data.growInto != null ? Mathf.Max(Now + data.growTime, 0.001f) : 0f;
        worldState.GetChunk(coord).SetGrowTime(index, time);
    }

    private void OnPropDepleted(Vector2Int coord, PropHealth prop, Vector3 position)
    {
        PropData data = prop.Data;
        if (data == null) return;

        if (data.leaveBehind != null && !prop.BurnedOut) Place(coord, data.leaveBehind, position);

        // 경계 밖 청크는 언로드 시 원래대로 돌아가므로 예약하지 않음
        if (data.regrowAs != null && worldState.IsInside(coord))
        {
            worldState.GetChunk(coord).AddRegen(worldState.GetPropType(data.regrowAs), Now + data.regrowDelay);
        }
    }

    // 시간이 된 변화와 재생을 처리. 화면에 보이거나 자리를 못 찾으면 다음 검사로 미룸
    private void Process(Vector2Int coord)
    {
        ChunkData chunk = worldState.GetChunk(coord);
        float now = Now;

        // 종류만 바꾸므로 번호는 그대로 유지됨
        foreach (int index in chunk.DueGrowths(now))
        {
            PropData data = DataOf(worldState.GetPropPrefab(chunk.types[index]));
            if (data == null || data.growInto == null)
            {
                chunk.SetGrowTime(index, 0f);
                continue;
            }
            if (IsVisible(chunk.PropPosition(index))) continue;

            spawner.ReplaceProp(coord, index, data.growInto);
            ScheduleGrowth(coord, index, data.growInto);
        }

        for (int i = chunk.RegenCount() - 1; i >= 0; i--)
        {
            if (chunk.regenAt[i] > now) continue;

            GameObject prefab = worldState.GetPropPrefab(chunk.regenTypes[i]);
            if (prefab == null)
            {
                chunk.RemoveRegenAt(i);
                continue;
            }

            if (!TryFindSpotInChunk(coord, prefab, out Vector3 position)) continue;

            chunk.RemoveRegenAt(i);
            Place(coord, prefab, position);
        }
    }

    private bool TryFindSpotInChunk(Vector2Int coord, GameObject prefab, out Vector3 position)
    {
        Vector3 origin = worldState.ChunkOrigin(coord);
        float size = worldState.ChunkSize;
        float spacing = DataOf(prefab)?.spacing ?? 0f;

        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            position = Round(origin + new Vector3(Random.value * size, 0f, Random.value * size));
            if (IsFreeSpot(position, spacing, true)) return true;
        }

        position = default;
        return false;
    }

    // 세이브 크기를 줄이기 위해 월드 생성과 같이 소수 둘째 자리까지만 사용
    public static Vector3 Round(Vector3 position)
    {
        return new Vector3(Mathf.Round(position.x * 100f) / 100f, 0f, Mathf.Round(position.z * 100f) / 100f);
    }

    // 새 자원이 생길 수 있는 자리인지 (경계 안, 다른 자원과 spacing 이상, 설치물·플레이어·몬스터와 떨어짐)
    // 설치물은 청크와 상관없이 항상 배치되어 있어 로드되지 않은 곳도 물리 검사로 확인 가능
    public bool IsFreeSpot(Vector3 position, float spacing, bool requireHidden)
    {
        if (!worldState.IsInside(position)) return false;
        if (requireHidden && IsVisible(position)) return false;
        if (HasPropNear(position, spacing)) return false;
        if (worldState.IsInStructure(position)) return false;

        if (structureClearance > 0f &&
            Physics.CheckSphere(position, structureClearance, structureLayers, QueryTriggerInteraction.Ignore)) return false;
        if (blockingRadius > 0f &&
            Physics.CheckSphere(position + Vector3.up * blockingRadius, blockingRadius, blockingLayers, QueryTriggerInteraction.Ignore)) return false;

        return true;
    }

    // 청크 데이터 기준이라 로드되지 않은 청크의 자원도 확인함 (경계 안 청크만)
    // 바닥을 넓게 차지하는 자원(PropFootprint) 근처는 distance보다 멀어도 막음
    private bool HasPropNear(Vector3 position, float distance)
    {
        float range = Mathf.Max(distance, PropFootprint.MaxRadius);
        Vector3 reach = new(range, 0f, range);
        Vector2Int min = worldState.WorldToChunk(position - reach);
        Vector2Int max = worldState.WorldToChunk(position + reach);

        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                var coord = new Vector2Int(x, z);
                if (!worldState.IsInside(coord)) continue;

                ChunkData chunk = worldState.GetChunk(coord);
                for (int i = 0; i < chunk.PropCount(); i++)
                {
                    float dx = chunk.px[i] - position.x;
                    float dz = chunk.pz[i] - position.z;
                    float limit = Mathf.Max(distance, PropFootprint.RadiusOf(worldState.GetPropPrefab(chunk.types[i]),
                        new Vector3(chunk.px[i], 0f, chunk.pz[i])));
                    if (dx * dx + dz * dz < limit * limit) return true;
                }
            }
        }
        return false;
    }

    private bool IsVisible(Vector3 position)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        return InView(cam, position) || InView(cam, position + Vector3.up * propHeight);
    }

    private bool InView(Camera cam, Vector3 point)
    {
        Vector3 v = cam.WorldToViewportPoint(point);
        return v.z > 0f && v.x >= -viewMargin && v.x <= 1f + viewMargin && v.y >= -viewMargin && v.y <= 1f + viewMargin;
    }
}
