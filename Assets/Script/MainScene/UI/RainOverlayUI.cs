using UnityEngine;
using UnityEngine.UI;

// 비가 오는 동안 화면 전체에 빗줄기를 그림 (비의 세기만큼 진하게)
// 설정의 '비 효과'를 끄면 표시하지 않음 (GameOptions.ShowRainEffect). 비 오는 날 어두워지는 것은 LightingSystem이 따로 처리
// 빗줄기 그림은 코드로 만든 임시 픽셀 무늬를 반복해서 깔고, 무늬를 비스듬히 흘려 내림
[RequireComponent(typeof(RawImage))]
public class RainOverlayUI : MonoBehaviour
{
    [SerializeField] private Color color = new(0.75f, 0.85f, 1f, 0.55f);
    [Tooltip("무늬 한 장의 픽셀 수 (가로·세로)")]
    [SerializeField, Range(16, 128)] private int tileSize = 48;
    [Tooltip("무늬 한 장에 그리는 빗줄기 수")]
    [SerializeField, Range(1, 64)] private int streaksPerTile = 14;
    [Tooltip("무늬 1픽셀이 화면에서 차지하는 픽셀 수 (픽셀아트 크기에 맞춤)")]
    [SerializeField, Range(1, 8)] private int pixelScale = 3;
    [Tooltip("빗줄기가 흘러내리는 속도 (초당 무늬 장수, x: 왼쪽, y: 아래)")]
    [SerializeField] private Vector2 fallSpeed = new(0.6f, 2.4f);

    private RawImage image;
    private Texture2D texture;
    private Vector2 offset;

    private void Awake()
    {
        image = GetComponent<RawImage>();
        image.raycastTarget = false;
        texture = BuildTexture();
        image.texture = texture;
        image.enabled = false;
    }

    private void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }

    private void Update()
    {
        WeatherSystem weather = WeatherSystem.Instance;
        float strength = weather != null && GameOptions.ShowRainEffect ? weather.RainFactor : 0f;

        image.enabled = strength > 0.001f;
        if (!image.enabled) return;

        image.color = new Color(color.r, color.g, color.b, color.a * strength);

        // 화면 크기에 맞춰 무늬를 반복하고, 위에서 아래로(조금 왼쪽으로) 흘림
        offset += new Vector2(fallSpeed.x, fallSpeed.y) * Time.deltaTime;
        offset.x = Mathf.Repeat(offset.x, 1f);
        offset.y = Mathf.Repeat(offset.y, 1f);
        float tilePixels = tileSize * pixelScale;
        image.uvRect = new Rect(offset.x, offset.y, Screen.width / tilePixels, Screen.height / tilePixels);
    }

    // 짧은 사선 빗줄기를 흩뿌린 반복 무늬 (색은 RawImage.color로 입힘)
    private Texture2D BuildTexture()
    {
        var tex = new Texture2D(tileSize, tileSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
        };

        var pixels = new Color32[tileSize * tileSize];
        var rng = new System.Random(7);
        for (int i = 0; i < streaksPerTile; i++)
        {
            int x = rng.Next(tileSize);
            int y = rng.Next(tileSize);
            int length = 3 + rng.Next(3);
            byte alpha = (byte)(150 + rng.Next(106));
            // 위쪽 끝이 오른쪽으로 기울어진 빗줄기 (아래로 갈수록 왼쪽)
            for (int k = 0; k < length; k++)
            {
                int px = (x + k / 2) % tileSize;
                int py = (y + k) % tileSize;
                pixels[py * tileSize + px] = new Color32(255, 255, 255, alpha);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
