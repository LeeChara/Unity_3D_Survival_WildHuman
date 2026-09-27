using System.Collections.Generic;
using UnityEngine;

// 일정 주기마다 플레이어 주변 로드된 청크에 몬스터 스폰을 시도
// 스폰 지점의 바이옴 monsterSpawnTable 가중치에 따라 몬스터 종류를 결정
// 청크 로드 범위 밖으로 벗어난 몬스터는 제거
public class MonsterSpawner : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private ChunkStreamer chunkStreamer;

    [Header("스폰")]
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private int maxActiveMonsters = 10;
    [SerializeField] private float minSpawnDistance = 20f;
    [SerializeField] private float maxSpawnDistance = 30f;

    // 유효한 스폰 지점을 찾기 위한 최대 시도 횟수
    [SerializeField] private int maxSpawnAttempts = 10;

    [Header("스폰 지점 검사")]
    // 이 레이어와 겹치는 지점에는 스폰하지 않음 (Nothing이면 검사 생략)
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float spawnClearance = 1f;

    [Header("제거")]
    [SerializeField] private float despawnCheckInterval = 1f;

    private readonly HashSet<MonsterAI> activeMonsters = new();
    private readonly List<MonsterAI> despawnBuffer = new();

    private float spawnTimer;
    private float despawnTimer;

    private void Update()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
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
        if (activeMonsters.Count >= maxActiveMonsters) return;
        if (!TryFindSpawnPoint(out Vector3 position, out BiomeData biome)) return;

        MonsterAI prefab = PickMonster(biome.monsterSpawnTable);
        if (prefab == null) return;

        MonsterAI monster = Instantiate(prefab, position, Quaternion.identity, transform);
        activeMonsters.Add(monster);

        // 사망 시 MonsterHealth가 직접 Destroy하므로 목록에서만 제거
        if (monster.TryGetComponent(out Health health))
        {
            health.Died += () => activeMonsters.Remove(monster);
        }
    }

    private bool TryFindSpawnPoint(out Vector3 position, out BiomeData biome)
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // 플레이어 기준 랜덤 방향 + 랜덤 거리
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
            position = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance; 
            position.y = 0f;

            Vector2Int coord = chunkStreamer.WorldToChunkCoord(position);
            if (!chunkStreamer.IsChunkActive(coord)) continue;

            biome = chunkStreamer.GetBiome(coord);
            if (biome == null || biome.monsterSpawnTable == null || biome.monsterSpawnTable.Length == 0) continue;

            if (obstacleLayer != 0 &&
                Physics.CheckSphere(position + Vector3.up * spawnClearance, spawnClearance, obstacleLayer)) continue;

            return true;
        }

        position = default;
        biome = null;
        return false;
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
            activeMonsters.Remove(monster);
            if (monster != null) Destroy(monster.gameObject);
        }
        despawnBuffer.Clear();
    }
}
