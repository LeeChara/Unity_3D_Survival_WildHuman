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
}

[Serializable]
public class SpawnRule
{
    public float spawnInterval = 3f;
    public int maxActiveMonsters = 20;
    public float minSpawnDistance = 150f;
    public float maxSpawnDistance = 250f;
}
