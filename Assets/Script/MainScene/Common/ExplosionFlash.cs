using UnityEngine;

// 폭발 순간의 임시 연출: 스프라이트가 커지면서 사라지고, 광원도 함께 어두워진 뒤 스스로 제거됨
// 전용 이펙트 시스템이 생기면 교체 예정
public class ExplosionFlash : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 0.4f;
    [SerializeField] private SpriteRenderer sprite;
    [Tooltip("시작·끝 크기 (스프라이트 Transform의 원래 크기에 곱함)")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.5f, 1.5f);
    [SerializeField] private LightSource flashLight;
    [SerializeField] private float lightRadius = 8f;
    [SerializeField] private float lightIntensity = 3f;
    [SerializeField] private Color lightColor = new Color(1f, 0.7f, 0.35f);

    private Vector3 baseScale;
    private Color baseColor;
    private float elapsed;

    private void Awake()
    {
        baseScale = sprite.transform.localScale;
        baseColor = sprite.color;
        Apply(0f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        Apply(t);

        if (t >= 1f) Destroy(gameObject);
    }

    private void Apply(float t)
    {
        sprite.transform.localScale = baseScale * Mathf.Lerp(scaleRange.x, scaleRange.y, t);

        Color color = baseColor;
        color.a *= 1f - t;
        sprite.color = color;

        if (flashLight != null) flashLight.Configure(lightRadius * (1f - t * 0.5f), lightIntensity * (1f - t), lightColor, 0f);
    }
}
