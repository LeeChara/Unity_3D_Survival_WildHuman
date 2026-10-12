using UnityEngine;

// 기절 중 머리 위에서 도는 별 (프레임 반복 재생)
// StunIndicator가 기절하는 동안 대상의 자식으로 붙였다가 풀리면 제거
[RequireComponent(typeof(SpriteRenderer))]
public class StunEffect : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [Min(0.1f)] [SerializeField] private float frameRate = 8f;

    private SpriteRenderer spriteRenderer;
    private float elapsed;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (frames != null && frames.Length > 0) spriteRenderer.sprite = frames[0];
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;

        elapsed += Time.deltaTime;
        spriteRenderer.sprite = frames[Mathf.FloorToInt(elapsed * frameRate) % frames.Length];
    }
}
