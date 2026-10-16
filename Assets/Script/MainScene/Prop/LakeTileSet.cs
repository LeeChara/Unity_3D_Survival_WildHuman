using UnityEngine;

// 호수(용암·물) 타일맵에 쓰는 그림 (칸마다 꽉 찬 사각형 방식)
// 원본 그리는 법 (interiorSheet):
// - 한 프레임 = (pattern × tilePixels) 정사각형, 위·아래·좌·우가 이어지게 (이 크기마다 반복)
// - 프레임을 가로로 나란히 (예: 64×64 4프레임 → 256×64)
// 실행 중에 칸 크기로 잘라 쓰므로 베이크나 슬라이스는 필요 없음 (임포트 설정은 Point 필터만 맞추면 됨)
[CreateAssetMenu(fileName = "LakeTileSet", menuName = "Prop/LakeTileSet")]
public class LakeTileSet : ScriptableObject
{
    public Texture2D interiorSheet;
    [Min(1)] public int frameCount = 4;

    [Header("크기")]
    [Tooltip("타일 한 칸의 픽셀 수")]
    [Min(1)] public int tilePixels = 16;
    [Tooltip("무늬 한 장이 가로·세로 몇 칸인지 (무늬는 이 칸 수마다 반복)")]
    [Min(1)] public int pattern = 4;
    [Tooltip("1픽셀의 월드 크기 (m) — 다른 그림과 같게 0.1")]
    [Min(0.01f)] public float metersPerPixel = 0.1f;

    [Header("애니메이션")]
    [Min(0.1f)] public float framesPerSecond = 4f;

    public float CellSize => tilePixels * metersPerPixel;

    private Sprite[] sprites;

    // 무늬 위치 sub(왼쪽 아래부터 줄 단위), 프레임 f의 칸
    public Sprite GetSprite(int sub, int frame)
    {
        if (sprites == null || sprites.Length == 0) CreateSprites();
        return sprites[sub * frameCount + frame];
    }

    // 원본을 칸 단위 스프라이트로 나눔 (에셋으로 자르지 않고 처음 쓸 때 한 번 만듦)
    private void CreateSprites()
    {
        int subs = pattern * pattern;
        int patternPixels = pattern * tilePixels;
        float pixelsPerUnit = 1f / metersPerPixel;
        sprites = new Sprite[subs * frameCount];

        for (int sub = 0; sub < subs; sub++)
        {
            int x = sub % pattern * tilePixels;
            int y = sub / pattern * tilePixels;
            for (int frame = 0; frame < frameCount; frame++)
            {
                var rect = new Rect(frame * patternPixels + x, y, tilePixels, tilePixels);
                // 타일맵 기본 앵커(칸 중심)에 맞게 피벗도 중심
                Sprite sprite = Sprite.Create(interiorSheet, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = $"{name}_{sub}_{frame}";
                sprites[sub * frameCount + frame] = sprite;
            }
        }
    }

    // 원본이 바뀌면 다시 자르도록
    private void OnValidate()
    {
        sprites = null;
    }
}
