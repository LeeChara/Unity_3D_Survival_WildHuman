using System;
using UnityEngine;

// 월드 생성 전체 설정 (생성 단계 목록, 맵 크기·난이도 선택지)
// 월드 선택 씬(새 월드 생성)과 MainScene(경계 밖 즉석 생성)이 같은 에셋을 참조
[CreateAssetMenu(fileName = "WorldGenConfig", menuName = "World/WorldGenConfig")]
public class WorldGenConfig : ScriptableObject
{
    [Tooltip("생성 로직이 바뀌면 올림. 월드마다 기록되어 이후 기존 월드 보정에 사용")]
    public int genVersion = 1;

    public BiomeDatabase biomeDatabase;
    public BiomeGridSetting grid;

    [Tooltip("위에서부터 순서대로 실행 (청크마다 GenerateChunk → 맵 전체 GenerateMap)")]
    public WorldGenStep[] steps;

    [Header("맵 설정 선택지")]
    public MapSizeOption[] mapSizes =
    {
        new() { id = "small", displayName = "작게", chunks = 16 },
        new() { id = "medium", displayName = "보통", chunks = 24 },
        new() { id = "large", displayName = "크게", chunks = 32 },
    };

    public DifficultyOption[] difficulties =
    {
        new() { id = "easy", displayName = "쉬움" },
        new() { id = "normal", displayName = "보통" },
        new() { id = "hard", displayName = "어려움" },
    };

    [Header("에디터에서 MainScene을 바로 실행할 때 (저장하지 않는 임시 월드)")]
    public WorldGenSettings editorSettings = new() { seed = 814, mapSize = "large", difficulty = "normal" };

    public int IndexOfMapSize(string id) => Mathf.Max(0, System.Array.FindIndex(mapSizes, option => option.id == id));
    public int IndexOfDifficulty(string id) => Mathf.Max(0, System.Array.FindIndex(difficulties, option => option.id == id));

    // 화면 표시용 이름 (없는 id면 id 그대로)
    public string DifficultyName(string id)
    {
        foreach (var option in difficulties)
        {
            if (option.id == id) return option.displayName;
        }
        return id;
    }

    // 없는 id면 첫 번째 선택지 (이름이 바뀐 옛 세이브 등)
    public MapSizeOption GetMapSize(string id)
    {
        foreach (var option in mapSizes)
        {
            if (option.id == id) return option;
        }

        Debug.LogWarning($"[WorldGenConfig] 맵 크기 '{id}'가 없어 '{mapSizes[0].id}'로 대체");
        return mapSizes[0];
    }
}

[Serializable]
public class MapSizeOption
{
    [Tooltip("세이브에 기록되는 값 (한 번 정하면 변경하지 않음)")]
    public string id;
    public string displayName;
    [Tooltip("한 변의 청크 수")]
    [Min(2)] public int chunks;
}

[Serializable]
public class DifficultyOption
{
    [Tooltip("세이브에 기록되는 값 (한 번 정하면 변경하지 않음)")]
    public string id;
    public string displayName;
}
