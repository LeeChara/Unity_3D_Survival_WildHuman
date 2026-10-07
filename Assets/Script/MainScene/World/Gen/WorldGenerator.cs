using System.Diagnostics;
using Debug = UnityEngine.Debug;

// 맵 설정값으로 경계 안 맵 전체를 생성 (월드를 만들 때 한 번만, 이후에는 저장된 MapData를 씀)
public static class WorldGenerator
{
    public static MapData Generate(WorldGenConfig config, WorldGenSettings settings)
    {
        var stopwatch = Stopwatch.StartNew();

        int size = config.GetMapSize(settings.mapSize).chunks;
        MapData map = MapData.Create(size, size);
        var ctx = new WorldGenContext(config, settings.seed, map);

        // 청크 단계: 청크마다 모든 단계를 순서대로 (각 청크는 자기 난수만 쓰므로 순서와 무관하게 같은 결과)
        int propCount = 0;
        for (int i = 0; i < map.chunks.Length; i++)
        {
            ChunkData chunk = map.chunks[i];
            var coord = map.IndexToCoord(i);
            foreach (var step in config.steps)
            {
                step.GenerateChunk(ctx, coord, chunk);
            }
            propCount += chunk.PropCount();
        }

        // 맵 단계: 맵 전체를 보고 결정하는 것 (시작 지점, 이후 여러 청크에 걸친 구조물 등)
        foreach (var step in config.steps)
        {
            step.GenerateMap(ctx);
        }

        Debug.Log($"[WorldGenerator] 시드 {settings.seed}, {size}x{size} 청크, 자원 {propCount}개 생성 ({stopwatch.ElapsedMilliseconds}ms)");
        return map;
    }
}
