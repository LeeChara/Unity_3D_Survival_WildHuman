using UnityEngine;

// 바닥에 그리는 공격 범위 표시
// 예고 시간 동안 안쪽이 차오르고, 다 차면 공격이 나가는 시점. 공격 뒤 Release로 서서히 사라짐
// 모양과 위치는 AttackShape 판정과 같은 기준 (직선은 기준점에서 정면으로 뻗음)
public class AttackIndicator : MonoBehaviour
{
    private static readonly int ShapeId = Shader.PropertyToID("_Shape");
    private static readonly int SizeId = Shader.PropertyToID("_Size");
    private static readonly int InnerId = Shader.PropertyToID("_Inner");
    private static readonly int HalfAngleId = Shader.PropertyToID("_HalfAngle");
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
    private static readonly int AreaColorId = Shader.PropertyToID("_AreaColor");
    private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    private static Sprite quadSprite;

    private AttackIndicatorSetting setting;
    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock block;

    private float duration;
    private float elapsed;
    private bool released;
    private float fadeElapsed;

    // 다 차올랐는지 (공격이 나갈 시점)
    public bool IsFull => elapsed >= duration;

    // duration: 다 차오를 때까지의 시간 (0이면 바로 가득 찬 상태)
    public static AttackIndicator Show(AttackIndicatorSetting setting, AttackShape shape, Vector3 origin, Vector3 forward, float duration)
    {
        if (quadSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            quadSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                tex.width, 0, SpriteMeshType.FullRect);
        }

        var go = new GameObject($"AttackIndicator ({shape.kind})");
        var indicator = go.AddComponent<AttackIndicator>();
        indicator.Init(setting, shape, origin, forward, duration);
        return indicator;
    }

    private void Init(AttackIndicatorSetting setting, AttackShape shape, Vector3 origin, Vector3 forward, float duration)
    {
        this.setting = setting;
        this.duration = Mathf.Max(0f, duration);

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = quadSprite;
        spriteRenderer.sharedMaterial = setting.material;
        // 호수·용암 타일(short.MinValue)과 음파 고리 위, 모든 세워진 스프라이트 아래
        spriteRenderer.sortingOrder = short.MinValue + 2;
        spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        spriteRenderer.receiveShadows = false;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        // 바닥에 눕힌 뒤(X 90도) 스프라이트의 위쪽(Y)이 정면을 향하도록 회전
        Vector3 center = origin;
        Vector2 size;
        if (shape.IsLine)
        {
            // 직선은 기준점에서 정면으로 뻗으므로 사각형 중심은 길이의 절반 앞
            center += forward * (shape.length * 0.5f);
            size = new Vector2(shape.width, shape.length);
        }
        else
        {
            size = Vector2.one * (shape.radius * 2f);
        }
        transform.SetPositionAndRotation(new Vector3(center.x, setting.lift, center.z),
            Quaternion.Euler(90f, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 0f));
        transform.localScale = new Vector3(size.x, size.y, 1f);

        block = new MaterialPropertyBlock();
        block.SetFloat(ShapeId, shape.IsLine ? 1f : 0f);
        block.SetVector(SizeId, size);
        block.SetFloat(InnerId, shape.radius > 0f ? shape.Inner / shape.radius : 0f);
        block.SetFloat(HalfAngleId, shape.HalfAngle);
        block.SetFloat(PixelSizeId, setting.pixelSize);
        block.SetColor(AreaColorId, setting.areaColor);
        block.SetColor(FillColorId, setting.fillColor);
        block.SetColor(EdgeColorId, setting.edgeColor);
        Apply(this.duration > 0f ? 0f : 1f, 1f);
    }

    private void Update()
    {
        if (released)
        {
            fadeElapsed += Time.deltaTime;
            float t = fadeElapsed / setting.fadeDuration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Apply(1f, 1f - t);
            return;
        }

        elapsed += Time.deltaTime;
        Apply(duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f, 1f);
    }

    // 공격이 나간 뒤(또는 취소) 서서히 사라지고 스스로 제거
    public void Release()
    {
        if (released) return;
        released = true;
        fadeElapsed = 0f;
    }

    private void Apply(float progress, float alpha)
    {
        block.SetFloat(ProgressId, progress);
        block.SetFloat(AlphaId, alpha);
        spriteRenderer.SetPropertyBlock(block);
    }
}
