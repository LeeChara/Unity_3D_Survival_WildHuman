using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

// 일정 시간마다 무작위로 일어나는 지진
// 플레이어에서 distanceRange만큼 떨어진 곳이 진원이 되고, 규모에 비례한 반경 안에 그 바이옴 지진 테이블의 자원(바위·광물)이 솟아오름
// 진원이 로드되지 않은 먼 곳이어도 청크 데이터에 바로 추가되므로 나중에 가 보면 생겨 있음
// 화면 흔들림·소리는 진원에서 멀수록 약해지고, 지진이 이어지는 동안 자원이 하나씩 솟아오름
public class EarthquakeEvent : MonoBehaviour, ISaveParticipant
{
    private const string SaveKey = "earthquake";

    [SerializeField] private DayNightCycle clock;
    [SerializeField] private WorldState worldState;
    [SerializeField] private PropLifecycle lifecycle;
    [SerializeField] private Transform player;

    [Header("주기")]
    [Tooltip("다음 지진까지 걸리는 월드 시간(초) 범위 (하루 = 600초)")]
    [SerializeField] private Vector2 intervalRange = new(600f, 1800f);

    [Header("진원")]
    [Tooltip("플레이어에서 진원까지의 거리 범위")]
    [SerializeField] private Vector2 distanceRange = new(30f, 120f);

    [Header("규모")]
    [SerializeField] private Vector2 magnitudeRange = new(1f, 3f);
    [Tooltip("규모 1당 자원이 솟아오르는 반경")]
    [SerializeField, Min(0f)] private float radiusPerMagnitude = 6f;
    [Tooltip("규모 1당 솟아오르는 자원 수")]
    [SerializeField, Min(0f)] private float spawnsPerMagnitude = 3f;
    [Tooltip("지진 지속 시간 = 1초 + 규모 × 이 값")]
    [SerializeField, Min(0f)] private float durationPerMagnitude = 1.5f;
    [Tooltip("반경 안에 지진 자원이 이미 규모 × 이 값 이상 있으면 그만큼 덜 만듦 (같은 곳에 쌓이는 것 방지)")]
    [SerializeField, Min(0f)] private float maxNearbyPerMagnitude = 4f;
    [Tooltip("자원 하나를 놓을 자리를 찾기 위한 최대 시도 횟수")]
    [SerializeField, Min(1)] private int placementAttempts = 10;

    [Header("연출")]
    [Tooltip("진원 바로 옆에서 느끼는 규모 1당 화면 흔들림 세기")]
    [SerializeField, Min(0f)] private float shakePerMagnitude = 0.08f;
    [Tooltip("진원에서 이 거리 이상 떨어지면 흔들림·소리가 없음")]
    [SerializeField, Min(1f)] private float feltDistance = 200f;
    [Tooltip("SFX 믹서 그룹으로 출력하는 2D AudioSource")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("비워두면 소리 없이 진행")]
    [SerializeField] private AudioClip sound;
    [Tooltip("자원이 솟아오를 때 그 자리에 만들 이펙트 (비워두면 생성하지 않음, 제거는 이펙트 프리팹이 담당)")]
    [SerializeField] private GameObject spawnEffectPrefab;
    [Tooltip("플레이어에서 이 거리 안에서 솟아오를 때만 이펙트를 만듦")]
    [SerializeField, Min(0f)] private float effectDistance = 60f;
    [Tooltip("이펙트를 자원 위치에서 카메라 쪽으로 당겨 생성하는 거리")]
    [SerializeField, Min(0f)] private float effectForwardOffset = 0.6f;

    // 다음 지진 월드 시간 (음수면 아직 정하지 않음)
    private float nextAt = -1f;
    private bool quaking;

    private void Update()
    {
        if (nextAt < 0f) Schedule();
        else if (!quaking && clock.WorldTime >= nextAt) Trigger();
    }

    private void Schedule()
    {
        nextAt = clock.WorldTime + Random.Range(intervalRange.x, intervalRange.y);
    }

    // 테스트용: 인스펙터의 컴포넌트 메뉴에서 바로 발생
    [ContextMenu("지금 지진 발생")]
    public void Trigger()
    {
        if (quaking) return;
        Schedule();

        float magnitude = Random.Range(magnitudeRange.x, magnitudeRange.y);
        float radius = magnitude * radiusPerMagnitude;

        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction == Vector2.zero) direction = Vector2.right;
        Vector3 epicenter = player.position + new Vector3(direction.x, 0f, direction.y) * Random.Range(distanceRange.x, distanceRange.y);
        epicenter = worldState.ClampInside(epicenter, radius);
        epicenter.y = 0f;

        StartCoroutine(Quake(epicenter, magnitude, radius));
    }

    private IEnumerator Quake(Vector3 epicenter, float magnitude, float radius)
    {
        quaking = true;

        float duration = 1f + magnitude * durationPerMagnitude;
        Vector3 offset = player.position - epicenter;
        offset.y = 0f;
        float distance = offset.magnitude;
        float felt = 1f - Mathf.Clamp01(distance / feltDistance);

        if (felt > 0f)
        {
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(shakePerMagnitude * magnitude * felt, duration);
            if (audioSource != null && sound != null) audioSource.PlayOneShot(sound, felt);
        }

        int planned = Mathf.RoundToInt(magnitude * spawnsPerMagnitude);
        int room = Mathf.FloorToInt(magnitude * maxNearbyPerMagnitude) - CountQuakePropsNear(epicenter, radius);
        int count = Mathf.Clamp(planned, 0, Mathf.Max(room, 0));

        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(duration / (count + 1));
            if (TrySpawn(epicenter, radius)) spawned++;
        }

        Debug.Log($"[Earthquake] 규모 {magnitude:0.0}, 진원 ({epicenter.x:0}, {epicenter.z:0}), 플레이어와 {distance:0}m, 자원 {spawned}/{planned}개");
        quaking = false;
    }

    private bool TrySpawn(Vector3 epicenter, float radius)
    {
        for (int attempt = 0; attempt < placementAttempts; attempt++)
        {
            Vector2 circle = Random.insideUnitCircle * radius;
            Vector3 position = PropLifecycle.Round(epicenter + new Vector3(circle.x, 0f, circle.y));

            Vector2Int coord = worldState.WorldToChunk(position);
            if (!worldState.IsInside(coord)) continue;

            GameObject prefab = worldState.GetBiome(coord)?.PickEarthquakeProp(Random.value);
            if (prefab == null) continue;

            float spacing = PropLifecycle.DataOf(prefab)?.spacing ?? 0f;
            if (!lifecycle.IsFreeSpot(position, spacing, false)) continue;

            lifecycle.Place(coord, prefab, position);
            SpawnEffect(position);
            return true;
        }
        return false;
    }

    private void SpawnEffect(Vector3 position)
    {
        if (spawnEffectPrefab == null) return;

        Vector3 offset = player.position - position;
        offset.y = 0f;
        if (offset.sqrMagnitude > effectDistance * effectDistance) return;

        // 이펙트도 위치로 앞뒤를 정하므로(DepthSorter), 같은 자리에 솟은 자원보다 앞에 그려지도록 카메라 쪽으로 조금 당김
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 toCamera = -cam.transform.forward;
            toCamera.y = 0f;
            position += toCamera.normalized * effectForwardOffset;
        }

        Instantiate(spawnEffectPrefab, position, Quaternion.identity);
    }

    // 반경 안에 있는 지진 테이블 자원 수 (청크 데이터 기준이라 로드되지 않은 곳도 셈)
    private int CountQuakePropsNear(Vector3 center, float radius)
    {
        Vector3 reach = new(radius, 0f, radius);
        Vector2Int min = worldState.WorldToChunk(center - reach);
        Vector2Int max = worldState.WorldToChunk(center + reach);
        float radiusSqr = radius * radius;
        int count = 0;

        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                var coord = new Vector2Int(x, z);
                if (!worldState.IsInside(coord)) continue;

                ChunkData chunk = worldState.GetChunk(coord);
                BiomeData biome = worldState.GetBiome(coord);
                if (biome == null || biome.earthquakeTable == null) continue;

                for (int i = 0; i < chunk.PropCount(); i++)
                {
                    float dx = chunk.px[i] - center.x;
                    float dz = chunk.pz[i] - center.z;
                    if (dx * dx + dz * dz >= radiusSqr) continue;

                    GameObject prefab = worldState.GetPropPrefab(chunk.types[i]);
                    foreach (WeightedProp entry in biome.earthquakeTable)
                    {
                        if (entry.propPrefab == prefab)
                        {
                            count++;
                            break;
                        }
                    }
                }
            }
        }
        return count;
    }

    public void Capture(WorldSaveData data)
    {
        data.systems[SaveKey] = new JObject { ["nextAt"] = nextAt };
    }

    public void Restore(WorldSaveData data)
    {
        JToken saved = data.GetSystem(SaveKey);
        nextAt = saved?.Value<float?>("nextAt") ?? -1f;
    }
}
