using UnityEngine;
using UnityEngine.Tilemaps;

// 호수 칸 하나 (타일 세트마다 TileLake가 실행 중에 하나 만들어 모든 호수 칸에 씀)
// 칸 위치로 무늬의 어느 부분인지 정해서, 이웃 칸끼리 무늬가 이어지게 함
// 모든 칸이 같은 시각에 같은 프레임을 재생해야 무늬가 끊기지 않으므로 시작 시각은 모두 0
public class LakeTile : TileBase
{
    public LakeTileSet set;

    private Sprite[][] framesBySub;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = set.GetSprite(Sub(position), 0);
        tileData.color = Color.white;
        tileData.flags = TileFlags.LockAll;
        tileData.colliderType = Tile.ColliderType.None;
    }

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData)
    {
        if (set.frameCount <= 1) return false;

        tileAnimationData.animatedSprites = Frames(Sub(position));
        tileAnimationData.animationSpeed = set.framesPerSecond;
        tileAnimationData.animationStartTime = 0f;
        return true;
    }

    private int Sub(Vector3Int position)
    {
        int n = set.pattern;
        int x = ((position.x % n) + n) % n;
        int y = ((position.y % n) + n) % n;
        return y * n + x;
    }

    private Sprite[] Frames(int sub)
    {
        framesBySub ??= new Sprite[set.pattern * set.pattern][];
        if (framesBySub[sub] != null) return framesBySub[sub];

        var frames = new Sprite[set.frameCount];
        for (int f = 0; f < frames.Length; f++) frames[f] = set.GetSprite(sub, f);
        framesBySub[sub] = frames;
        return frames;
    }
}
