using UnityEngine;

// 지도 점·임시 아이콘용 도형 스프라이트 (코드로 한 번 만들어 공유)
public static class MapSprites
{
    private const int Size = 32;

    private static Sprite circle;
    private static Sprite diamond;
    private static Sprite playerMark;

    // 흰 원 (구조물·설치물 점)
    public static Sprite Circle => circle ??= Create("MapCircle", (x, y) => Distance(x, y) <= Size * 0.5f ? Color.white : Color.clear);

    // 흰 마름모 (스폰 지점 임시 아이콘)
    public static Sprite Diamond => diamond ??= Create("MapDiamond", (x, y) =>
        Mathf.Abs(x + 0.5f - Size * 0.5f) + Mathf.Abs(y + 0.5f - Size * 0.5f) <= Size * 0.5f ? Color.white : Color.clear);

    // 테두리가 어두운 흰 원 (플레이어 임시 아이콘)
    public static Sprite PlayerMark => playerMark ??= Create("MapPlayer", (x, y) =>
    {
        float d = Distance(x, y);
        if (d > Size * 0.5f) return Color.clear;
        return d > Size * 0.5f - 5f ? new Color(0.1f, 0.1f, 0.12f) : Color.white;
    });

    private static float Distance(int x, int y)
    {
        float dx = x + 0.5f - Size * 0.5f;
        float dy = y + 0.5f - Size * 0.5f;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static Sprite Create(string name, System.Func<int, int, Color> shade)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                pixels[x + y * Size] = shade(x, y);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }
}
