// 월드 선택 씬에서 고른 월드를 MainScene으로 넘기는 통로 (SceneFlow처럼 씬 사이에 유지되는 정적 상태)
// 비어 있으면(에디터에서 MainScene을 바로 실행) MainScene이 저장하지 않는 임시 월드를 만들어 씀
public static class WorldSession
{
    public static WorldMeta Meta { get; private set; }
    public static WorldSaveData Data { get; private set; }

    public static bool HasWorld => Meta != null && Data != null;

    public static void Begin(WorldMeta meta, WorldSaveData data)
    {
        Meta = meta;
        Data = data;
    }

    public static void Clear()
    {
        Meta = null;
        Data = null;
    }
}
