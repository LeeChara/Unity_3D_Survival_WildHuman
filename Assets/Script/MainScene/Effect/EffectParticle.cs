using UnityEngine;

// 파티클 하나의 상태와 움직임 계산 (그리는 방법과 무관)
// 게임(Effects)은 SpriteRenderer에, 이펙트 관리 창의 미리보기는 에디터 화면에 같은 계산 결과를 그림
// 좌표는 화면 평면 기준 (x: 화면 오른쪽, y: 위, 0이 바닥)
public class EffectParticle
{
    // 계단식으로 흐려지는 단계 수 (픽셀 그림이 부드럽게 번지지 않도록)
    private const int FadeSteps = 3;

    public ParticleEmitter emitter;
    public Vector2 position;
    public Vector2 velocity;
    public Color color;
    public float age;
    public float life;
    public float delay;
    public float size;
    public float wobblePhase;
    public bool grounded;
    // randomFrame일 때 고른 그림
    public int frame;

    public bool Waiting => delay > 0f;
    public bool Finished => age >= life;

    // 이미터 한 개에서 이번에 뿌릴 개수 (countScale: 설정의 '적게'면 0.5)
    public static int RollCount(ParticleEmitter emitter, float countScale)
    {
        return Mathf.CeilToInt(Random.Range(emitter.count.x, emitter.count.y + 1) * countScale);
    }

    // width·height: 기준 범위를 화면 평면에서 본 너비·높이 (점이면 0)
    public void Init(ParticleEmitter emitter, float width, float height, Color tint)
    {
        this.emitter = emitter;
        Vector2 areaSize = Vector2.Max(new Vector2(emitter.area.x * width, emitter.area.y * height), emitter.minArea);
        Vector2 areaCenter = new Vector2(emitter.center.x * width, emitter.center.y * height);

        position = areaCenter + new Vector2(Random.Range(-0.5f, 0.5f) * areaSize.x, Random.Range(-0.5f, 0.5f) * areaSize.y);
        if (emitter.stopOnGround) position.y = Mathf.Max(position.y, 0f);
        float angle = (emitter.angle + Random.Range(-0.5f, 0.5f) * emitter.spread) * Mathf.Deg2Rad;
        velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(emitter.speed.x, emitter.speed.y);
        Color baseColor = Color.Lerp(emitter.colorA, emitter.colorB, Random.value);
        color = emitter.useTint ? baseColor * tint : baseColor;
        age = 0f;
        life = Mathf.Max(0.05f, Random.Range(emitter.lifetime.x, emitter.lifetime.y));
        delay = Random.Range(emitter.delay.x, emitter.delay.y);
        size = Random.Range(emitter.size.x, emitter.size.y);
        wobblePhase = Random.value * Mathf.PI * 2f;
        grounded = false;
        frame = emitter.frames != null && emitter.frames.Length > 0 ? Random.Range(0, emitter.frames.Length) : 0;
    }

    // 대기 시간이 남았으면 줄이기만 하고, 끝났으면 나이를 먹고 움직임
    public void Tick(float dt)
    {
        if (delay > 0f)
        {
            delay -= dt;
            return;
        }

        age += dt;
        if (grounded) return;

        velocity.y -= emitter.gravity * dt;
        if (emitter.drag > 0f) velocity *= Mathf.Max(0f, 1f - emitter.drag * dt);
        position += velocity * dt;

        // 바닥에 닿으면 그 자리에 멈춤 (파편이 땅에 떨어진 느낌)
        if (emitter.stopOnGround && position.y <= 0f && velocity.y < 0f)
        {
            position.y = 0f;
            velocity = Vector2.zero;
            grounded = true;
        }
    }

    public Sprite CurrentSprite
    {
        get
        {
            Sprite[] frames = emitter.frames;
            if (frames == null || frames.Length == 0) return null;
            if (emitter.randomFrame) return frames[Mathf.Min(frame, frames.Length - 1)];
            if (emitter.loopFrames) return frames[Mathf.FloorToInt(age * emitter.frameRate) % frames.Length];
            return frames[Mathf.Min(Mathf.FloorToInt(LifeRatio * frames.Length), frames.Length - 1)];
        }
    }

    public float LifeRatio => Mathf.Clamp01(age / life);

    // 그릴 위치 (흔들림 포함)
    public Vector2 DrawPosition
    {
        get
        {
            float x = position.x;
            if (emitter.wobble > 0f && !grounded) x += Mathf.Sin(age * 6f + wobblePhase) * emitter.wobble;
            return new Vector2(x, position.y);
        }
    }

    public float DrawScale => size * emitter.sizeOverLife.Evaluate(LifeRatio);

    public Color DrawColor
    {
        get
        {
            Color c = color;
            c.a *= FadeAlpha(LifeRatio, emitter.fadeStart);
            return c;
        }
    }

    // fadeStart 이후를 FadeSteps 단계로 나눠 계단식으로 흐려짐
    private static float FadeAlpha(float t, float fadeStart)
    {
        if (t < fadeStart || fadeStart >= 1f) return 1f;

        float f = (t - fadeStart) / (1f - fadeStart);
        return 1f - Mathf.Ceil(f * FadeSteps) / (FadeSteps + 1);
    }
}
