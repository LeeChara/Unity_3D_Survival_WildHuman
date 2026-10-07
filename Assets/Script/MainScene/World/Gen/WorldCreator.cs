using System;

// 새 월드 만들기: 맵 전체를 생성해 바로 저장 (이후에는 저장된 맵으로만 플레이)
public static class WorldCreator
{
    public static (WorldMeta meta, WorldSaveData data) Create(WorldGenConfig config, string name, WorldGenSettings settings)
    {
        long now = DateTime.UtcNow.Ticks;
        var meta = new WorldMeta
        {
            id = SaveSystem.NewWorldId(),
            name = name,
            createdAt = now,
            lastPlayedAt = now,
            saveVersion = SaveSystem.SaveVersion,
            genVersion = config.genVersion,
            settings = settings,
        };
        var data = new WorldSaveData
        {
            saveVersion = SaveSystem.SaveVersion,
            map = WorldGenerator.Generate(config, settings),
        };

        SaveSystem.Write(meta, data);
        return (meta, data);
    }

    // 비우면 무작위, 정수면 그대로, 그 외 문자열은 해시 (같은 문자열이면 항상 같은 시드)
    public static int ParseSeed(string text)
    {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) return new Random().Next(int.MinValue, int.MaxValue);
        if (int.TryParse(text, out int seed)) return seed;
        return WorldGenContext.StableHash(text);
    }
}
