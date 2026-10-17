using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// 일정 주기마다 플레이어 주변 로드된 청크에 몬스터 스폰을 시도
// 스폰 지점의 바이옴 monsterSpawnTable 가중치에 따라 몬스터 종류를 결정
// 청크 로드 범위 밖으로 벗어난 몬스터는 제거
// 저장 시에는 지금 살아 있는 몬스터만 기록 (멀어져 제거된 몬스터는 남지 않음)
// 한 번에 여러 마리(무리)를 스폰하고, 밤에는 더 자주·더 많이 스폰 (NightFactor로 보간)
// 스폰된 개체는 크기가 조금씩 다름 (sizeScaleRange, 저장 시 함께 기록)
// 스포너가 관리하는 몬스터는 배회할 때 플레이어 쪽으로 기움 (문지기·보스 등 다른 경로로 만든 몬스터는 제외)
public class MonsterSpawner : MonoBehaviour, ISaveParticipant
{
    private const string SaveKey = "monsters";
    // 개체 기록의 data에 크기 배율을 넣을 때 쓰는 키
    private const string SizeScaleKey = "sizeScale";

    [SerializeField] private Transform player;
    [SerializeField] private ChunkStreamer chunkStreamer;
    [SerializeField] private DayNightCycle dayNightCycle;

    [Header("스폰")]
    // 스폰 주기·최대 수·거리 규칙
    [SerializeField] private GameSetting gameSetting;

    // 유효한 스폰 지점을 찾기 위한 최대 시도 횟수
    [SerializeField] private int maxSpawnAttempts = 10;
    // 무리의 각 개체가 첫 개체 주변에서 자리를 찾는 최대 시도 횟수
    [SerializeField] private int maxPackMemberAttempts = 4;
    [Tooltip("스폰된 몬스터의 크기 배율 범위 (프리팹 크기 기준)")]
    [SerializeField] private Vector2 sizeScaleRange = new(0.9f, 1.1f);

    [Header("스폰 지점 검사")]
    // 이 레이어와 겹치는 지점에는 스폰하지 않음 (Nothing이면 검사 생략)
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float spawnClearance = 1f;
    [Tooltip("이 레이어의 위험 지형(용암 호수 등) 안에는 스폰하지 않음")]
    [SerializeField] private LayerMask hazardLayers;

    [Header("제거")]
    [SerializeField] private float despawnCheckInterval = 1f;

    private SpawnRule Rule => gameSetting.spawn;

    private readonly HashSet<MonsterAI> activeMonsters = new();
    // 개체별 크기 배율 (저장 시 함께 기록해서 불러와도 크기가 유지되게)
    private readonly Dictionary<MonsterAI, float> sizeScales = new();
    private readonly List<MonsterAI> despawnBuffer = new();

    private float spawnTimer;
    private float despawnTimer;

    private float NightFactor => dayNightCycle != null ? dayNightCycle.NightFactor : 0f;

    // 배회 방향이 플레이어 쪽으로 기우는 정도 (MonsterAI가 배회 목적지를 고를 때 읽음)
    public float PlayerDrift => Mathf.Lerp(Rule.playerDrift, Rule.nightPlayerDrift, NightFactor);
    public float WanderCooldownScale => Mathf.Lerp(Rule.wanderCooldownScale, Rule.nightWanderCooldownScale, NightFactor);

    private float SpawnInterval => Mathf.Lerp(Rule.spawnInterval, Rule.nightSpawnInterval, NightFactor);
    private int MaxActiveMonsters => Mathf.RoundToInt(Mathf.Lerp(Rule.maxActiveMonsters, Rule.nightMaxActiveMonsters, NightFactor));

    private void Update()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= SpawnInterval)
        {
            spawnTimer = 0f;
            TrySpawn();
        }

        despawnTimer += Time.deltaTime;
        if (despawnTimer >= despawnCheckInterval)
        {
            despawnTimer = 0f;
            DespawnOutOfRange();
        }
    }

    private void TrySpawn()
    {
        int remaining = MaxActiveMonsters - activeMonsters.Count;
        if (remaining <= 0) return;
        if (!TryFindSpawnPoint(out Vector3 center, out BiomeData centerBiome)) return;

        int count = Mathf.Min(PickPackSize(), remaining);
        SpawnAt(center, centerBiome);

        // 나머지 개체는 첫 개체 주변에서 자리를 찾음 (각 자리의 바이옴 표를 따름)
        for (int i = 1; i < count; i++)
        {
            for (int attempt = 0; attempt < maxPackMemberAttempts; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * Rule.packRadius;
                Vector3 position = center + new Vector3(offset.x, 0f, offset.y);
                if (!IsValidSpawnPoint(position, out BiomeData biome)) continue;

                SpawnAt(position, biome);
                break;
            }
        }
    }

    // 낮/밤 범위를 보간한 뒤 그 안에서 무작위
    private int PickPackSize()
    {
        float t = NightFactor;
        int min = Mathf.RoundToInt(Mathf.Lerp(Rule.packSize.x, Rule.nightPackSize.x, t));
        int max = Mathf.RoundToInt(Mathf.Lerp(Rule.packSize.y, Rule.nightPackSize.y, t));
        return Mathf.Max(1, Random.Range(min, Mathf.Max(min, max) + 1));
    }

    private void SpawnAt(Vector3 position, BiomeData biome)
    {
        MonsterAI prefab = PickMonster(biome.monsterSpawnTable);
        if (prefab == null) return;

        MonsterAI monster = Instantiate(prefab, position, Quaternion.identity, transform);
        Register(monster, Random.Range(sizeScaleRange.x, sizeScaleRange.y));
    }

    private void Register(MonsterAI monster, float sizeScale)
    {
        // 프리팹 루트 크기에 곱함 (루트 크기가 1이 아닌 프리팹도 있음)
        monster.transform.localScale *= sizeScale;
        sizeScales[monster] = sizeScale;

        activeMonsters.Add(monster);
        monster.EnablePlayerDrift(player, this);

        // 사망 시 MonsterHealth가 직접 Destroy하므로 목록에서만 제거
        if (monster.TryGetComponent(out Health health))
        {
            health.Died += () => Unregister(monster);
        }
    }

    private void Unregister(MonsterAI monster)
    {
        activeMonsters.Remove(monster);
        sizeScales.Remove(monster);
    }

    public void Capture(WorldSaveData data)
    {
        var records = new List<EntityRecord>();
        foreach (var monster in activeMonsters)
        {
            if (monster == null || !monster.TryGetComponent(out SaveableEntity entity)) continue;
            // 시체로 남아 있는 몬스터는 저장하지 않음
            if (monster.TryGetComponent(out Health health) && health.IsDead) continue;

            EntityRecord record = entity.Capture();
            if (sizeScales.TryGetValue(monster, out float sizeScale)) record.data[SizeScaleKey] = sizeScale;
            records.Add(record);
        }
        data.entities[SaveKey] = records;
    }

    public void Restore(WorldSaveData data)
    {
        foreach (var record in data.GetEntities(SaveKey))
        {
            SaveableEntity prefab = SaveRegistry.Active.GetEntity(record.type);
            if (prefab == null || !prefab.TryGetComponent(out MonsterAI monsterPrefab)) continue;

            // Awake에서 최대 체력으로 초기화된 뒤 저장된 체력으로 덮어씀
            MonsterAI monster = Instantiate(monsterPrefab, record.Position(), Quaternion.identity, transform);
            monster.GetComponent<SaveableEntity>().LoadData(record);
            // 크기 기록이 없는 이전 세이브는 원래 크기로
            float sizeScale = record.data.TryGetValue(SizeScaleKey, out JToken token) ? token.Value<float>() : 1f;
            Register(monster, sizeScale);
        }
    }

    private bool TryFindSpawnPoint(out Vector3 position, out BiomeData biome)
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // 플레이어 기준 랜덤 방향 + 랜덤 거리
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(Rule.minSpawnDistance, Rule.maxSpawnDistance);
            position = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance; 
            position.y = 0f;

            if (IsValidSpawnPoint(position, out biome)) return true;
        }

        position = default;
        biome = null;
        return false;
    }

    private bool IsValidSpawnPoint(Vector3 position, out BiomeData biome)
    {
        biome = null;

        Vector2Int coord = chunkStreamer.WorldToChunkCoord(position);
        if (!chunkStreamer.IsChunkActive(coord)) return false;
        // 경계 밖 청크도 화면에는 보이지만 스폰하지 않음
        if (WorldState.Instance != null && !WorldState.Instance.IsInside(coord)) return false;

        biome = chunkStreamer.GetBiome(coord);
        if (biome == null || biome.monsterSpawnTable == null || biome.monsterSpawnTable.Length == 0) return false;

        if (obstacleLayer != 0 &&
            Physics.CheckSphere(position + Vector3.up * spawnClearance, spawnClearance, obstacleLayer)) return false;
        // 용암 호수 같은 위험 지형 안에는 스폰하지 않음
        if (HazardQuery.IsInside(position, hazardLayers)) return false;

        return true;
    }

    // 가중치 기반 랜덤 선택
    private MonsterAI PickMonster(MonsterSpawnEntry[] table)
    {
        float totalWeight = 0f;
        foreach (var entry in table)
        {
            if (entry.monsterPrefab != null) totalWeight += entry.weight;
        }
        if (totalWeight <= 0f) return null;

        float roll = Random.Range(0f, totalWeight);
        MonsterAI lastValid = null;
        foreach (var entry in table)
        {
            if (entry.monsterPrefab == null || entry.weight <= 0f) continue;

            lastValid = entry.monsterPrefab;
            roll -= entry.weight;
            if (roll < 0f) return entry.monsterPrefab;
        }

        // Random.Range(float)는 최댓값을 포함할 수 있어 마지막 항목으로 보정
        return lastValid;
    }

    private void DespawnOutOfRange()
    {
        foreach (var monster in activeMonsters)
        {
            // 외부에서 파괴된 경우도 정리
            if (monster == null)
            {
                despawnBuffer.Add(monster);
                continue;
            }

            Vector2Int coord = chunkStreamer.WorldToChunkCoord(monster.transform.position);
            if (!chunkStreamer.IsChunkActive(coord))
                despawnBuffer.Add(monster);
        }

        foreach (var monster in despawnBuffer)
        {
            Unregister(monster);
            if (monster != null) Destroy(monster.gameObject);
        }
        despawnBuffer.Clear();
    }
}
