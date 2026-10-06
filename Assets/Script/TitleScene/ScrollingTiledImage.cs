using UnityEngine;
using UnityEngine.UI;

// 텍스처를 일정 배율로 바둑판처럼 깔고 천천히 흘려보내는 배경 (텍스처 Wrap Mode는 Repeat이어야 함)
[RequireComponent(typeof(RawImage))]
public class ScrollingTiledImage : MonoBehaviour
{
    [Tooltip("텍스처 1픽셀을 화면(캔버스) 몇 픽셀로 그릴지")]
    [SerializeField] private float pixelScale = 3f;
    [Tooltip("초당 이동량 (텍스처 한 장 = 1)")]
    [SerializeField] private Vector2 velocity = new(0.01f, 0.006f);

    private RawImage image;
    private RectTransform rect;

    private void Awake()
    {
        image = GetComponent<RawImage>();
        rect = (RectTransform)transform;
    }

    private void Update()
    {
        Texture texture = image.texture;
        if (texture == null) return;

        // 해상도가 바뀌어도 배율이 유지되도록 매 프레임 영역 크기로 타일 수를 계산
        Vector2 size = rect.rect.size;
        Rect uv = image.uvRect;
        uv.width = size.x / (texture.width * pixelScale);
        uv.height = size.y / (texture.height * pixelScale);
        uv.x = Mathf.Repeat(uv.x + velocity.x * Time.unscaledDeltaTime, 1f);
        uv.y = Mathf.Repeat(uv.y + velocity.y * Time.unscaledDeltaTime, 1f);
        image.uvRect = uv;
    }
}
