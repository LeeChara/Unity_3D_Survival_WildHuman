using System;
using UnityEngine;

// 게임 전체의 규칙·페이스를 정하는 밸런스 값 모음
// 개체별 스탯(MonsterData 등), 지형 생성(BiomeGridSetting), 연출 설정은 각자의 에셋에 둠
[CreateAssetMenu(fileName = "GameSetting", menuName = "Common/GameSetting")]
public class GameSetting : ScriptableObject
{
    public DayNightRule dayNight = new();
    public HungerRule hunger = new();
    public SpawnRule spawn = new();
    public ItemRule item = new();
    public DeathRule death = new();
    public SaveRule save = new();
}

[Serializable]
public class DayNightRule
{
    [Tooltip("낮 지속 시간(초)")]
    public float dayDuration = 300f;
    [Tooltip("밤 지속 시간(초)")]
    public float nightDuration = 300f;
    [Tooltip("낮↔밤 전환 시 서서히 바뀌는 시간(초)")]
    public float transitionDuration = 20f;
}

[Serializable]
public class HungerRule
{
    [Tooltip("가득 찬 상태에서 0이 되기까지 걸리는 시간(초)")]
    public float timeToEmpty = 300f;
    [Tooltip("달리는 중 허기 감소 배율")]
    public float sprintDecayMultiplier = 1.5f;
    [Tooltip("굶주림 데미지 간격(초)")]
    public float starveInterval = 2f;
    public int starveDamage = 1;
    [Tooltip("허기가 최대치의 이 비율 이상이면 체력 회복")]
    [Range(0f, 1f)]
    public float regenThreshold = 0.9f;
    [Tooltip("체력 회복 간격(초)")]
    public float regenInterval = 2f;
    public float regenAmount = 1f;
}

// 낮/밤 값이 따로 있는 항목은 NightFactor(0~1)로 두 값 사이를 보간
[Serializable]
public class SpawnRule
{
    [Tooltip("낮 스폰 주기(초)")]
    public float spawnInterval = 2f;
    [Tooltip("밤 스폰 주기(초)")]
    public float nightSpawnInterval = 1f;
    [Tooltip("낮 최대 몬스터 수")]
    public int maxActiveMonsters = 40;
    [Tooltip("밤 최대 몬스터 수")]
    public int nightMaxActiveMonsters = 70;
    public float minSpawnDistance = 60f;
    public float maxSpawnDistance = 150f;

    [Header("무리")]
    [Tooltip("낮에 한 번에 스폰되는 수 (x=최소, y=최대)")]
    public Vector2Int packSize = new(1, 3);
    [Tooltip("밤에 한 번에 스폰되는 수 (x=최소, y=최대)")]
    public Vector2Int nightPackSize = new(2, 4);
    [Tooltip("무리가 첫 개체 주변에 흩어지는 반경")]
    public float packRadius = 6f;

    [Header("플레이어 쏠림 (스포너가 만든 몬스터만)")]
    [Tooltip("낮에 배회 방향이 플레이어 쪽으로 기우는 정도 (0=무작위, 1=곧장)")]
    [Range(0f, 1f)] public float playerDrift = 0.35f;
    [Tooltip("밤에 배회 방향이 플레이어 쪽으로 기우는 정도 (0=무작위, 1=곧장)")]
    [Range(0f, 1f)] public float nightPlayerDrift = 0.7f;
    [Tooltip("낮 배회 대기 시간 배율")]
    public float wanderCooldownScale = 1f;
    [Tooltip("밤 배회 대기 시간 배율")]
    public float nightWanderCooldownScale = 0.5f;
}

[Serializable]
public class ItemRule
{
    [Tooltip("땅에 떨어진 아이템이 사라지기까지 걸리는 시간(초)")]
    public float groundLifetime = 600f;
    [Tooltip("드랍 후 수집기가 끌어갈 수 있게 되기까지 대기 시간(초). 튀어 오르는 중에는 이 값과 관계없이 끌어가지 않음")]
    public float pickupDelay = 0.5f;
    [Tooltip("드랍 위치를 중심으로 흩어지는 반경")]
    public float scatterRadius = 1f;
    [Tooltip("드랍 시 튀어 오르는 높이")]
    public float popHeight = 1f;
    [Tooltip("드랍 시 튀어 올라 착지하기까지 걸리는 시간(초)")]
    public float popDuration = 0.4f;
    [Tooltip("인벤토리에서 던진 아이템이 날아가는 거리. 수집기의 끌어당김 반경보다 커야 바로 다시 끌려오지 않음")]
    public float throwDistance = 4f;
    [Tooltip("던진 아이템을 다시 주울 수 있게 되기까지 대기 시간(초)")]
    public float throwPickupDelay = 2f;
}

[Serializable]
public class DeathRule
{
    [Tooltip("사망 후 부활하기까지 걸리는 시간(초)")]
    public float respawnDelay = 5f;
    [Tooltip("사망 시 인벤토리의 모든 아이템을 사망 위치에 떨어뜨릴지 여부 (끄면 그대로 유지)")]
    public bool dropItemsOnDeath = true;
}

[Serializable]
public class SaveRule
{
    [Tooltip("자동 저장 간격(초)")]
    [Min(10f)] public float autosaveInterval = 300f;
}
