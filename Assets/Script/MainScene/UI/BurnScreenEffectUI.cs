using UnityEngine;
using UnityEngine.UI;

// 플레이어가 불타는 동안 화면 가장자리를 약하게 붉게 물들이고 천천히 맥박치듯 깜빡임
// 설정의 '화면 효과'를 끄면 표시하지 않음 (GameOptions.ShowScreenEffects)
// 테두리 그림은 픽셀아트에 맞춰 낮은 해상도로 직접 만들어 점 필터로 늘림
[RequireComponent(typeof(RawImage))]
public class BurnScreenEffectUI : MonoBehaviour
{
    [SerializeField] private Combustible target;
    [SerializeField] private Color color = new Color(1f, 0.35f, 0.1f, 1f);
    [Tooltip("깜빡일 때 가장 옅은·진한 불투명도")]
    [SerializeField] private Vector2 alphaRange = new Vector2(0.08f, 0.16f);
    [Tooltip("초당 깜빡임 횟수")]
    [SerializeField, Min(0f)] private float pulseRate = 1.2f;
    [Tooltip("켜지고 꺼지는 속도 (초당 불투명도 변화)")]
    [SerializeField, Min(0.1f)] private float fadeSpeed = 0.6f;
    [Tooltip("가장자리에서 안쪽으로 물드는 두께 (화면 비율)")]
    [SerializeField, Range(0.05f, 0.5f)] private float edgeWidth = 0.22f;
    [Tooltip("테두리 그림 가로 해상도 (세로는 4:3 비율)")]
    [SerializeField, Range(16, 128)] private int resolution = 64;

    private RawImage image;
    private Texture2D texture;
    private float alpha;
    private float time;

    private void Awake()
    {
        image = GetComponent<RawImage>();
        image.raycastTarget = false;
        texture = BuildTexture();
        image.texture = texture;
        SetAlpha(0f);
    }

    private void OnDestroy()
    {
        if (texture != null) Destroy(texture);
    }

    private void Update()
    {
        bool burning = GameOptions.ShowScreenEffects && target != null && target.IsBurning;

        time += Time.deltaTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * pulseRate * Mathf.PI * 2f);
        float goal = burning ? Mathf.Lerp(alphaRange.x, alphaRange.y, pulse) : 0f;
        alpha = Mathf.MoveTowards(alpha, goal, fadeSpeed * Time.deltaTime);
        SetAlpha(alpha);
    }

    private void SetAlpha(float value)
    {
        image.color = new Color(color.r, color.g, color.b, value);
        image.enabled = value > 0.001f;
    }

    // 가장자리일수록 불투명한 흰색 그림 (색은 RawImage.color로 입힘)
    private Texture2D BuildTexture()
    {
        int width = resolution;
        int height = Mathf.Max(1, Mathf.RoundToInt(resolution * 0.75f));
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 가장 가까운 가장자리까지의 거리 (0 = 가장자리, 0.5 = 중앙)
                float u = (x + 0.5f) / width;
                float v = (y + 0.5f) / height;
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float a = 1f - Mathf.Clamp01(edge / edgeWidth);
                a *= a;
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
