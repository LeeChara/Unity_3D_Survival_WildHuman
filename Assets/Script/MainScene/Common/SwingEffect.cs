using UnityEngine;

// 공격 판정이 켜져 있는 동안 히트박스 범위에 맞춰 바닥에 눕힌 스프라이트를 재생해 공격 범위를 보여줌
// AttackPivot 아래에 두면 클릭 방향을 따라 회전하고, 위치·크기는 히트박스(BoxCollider)에서 계산
// 프레임이 한 장뿐이면 프레임 애니메이션 대신 알파를 줄여 사라지게 함
[RequireComponent(typeof(SpriteRenderer))]
public class SwingEffect : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private PlayerAttack attack;
    [Tooltip("범위의 기준이 되는 히트박스")]
    [SerializeField] private BoxCollider hitbox;

    [Header("표시")]
    [Tooltip("휘두르기 프레임 (위쪽이 공격 방향, Attack 단계 동안 순서대로 재생)")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private Color color = Color.white;
    [Tooltip("판정 범위 대비 표시 크기 배율 (x: 좌우, y: 앞뒤)")]
    [SerializeField] private Vector2 sizeMultiplier = Vector2.one;
    [Tooltip("바닥과 겹쳐 깜빡이지 않도록 띄우는 높이 (월드 기준)")]
    [SerializeField] private float heightOffset = 0.01f;

    private SpriteRenderer spriteRenderer;
    private float elapsed;
    private bool isPlaying;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.enabled = false;
    }

    private void OnEnable()
    {
        attack.AttackStarted += Play;
        attack.AttackEnded += Stop;
    }

    private void OnDisable()
    {
        attack.AttackStarted -= Play;
        attack.AttackEnded -= Stop;

        Stop();
    }

    private void Update()
    {
        if (!isPlaying) return;

        elapsed += Time.deltaTime;
        float t = attack.attackDuration > 0f ? elapsed / attack.attackDuration : 1f;
        if (t >= 1f)
        {
            Stop();
            return;
        }

        if (frames.Length > 1)
        {
            int index = Mathf.Min(Mathf.FloorToInt(t * frames.Length), frames.Length - 1);
            spriteRenderer.sprite = frames[index];
        }
        else
        {
            Color faded = color;
            faded.a *= 1f - t;
            spriteRenderer.color = faded;
        }
    }

    private void Play()
    {
        if (frames == null || frames.Length == 0) return;

        spriteRenderer.sprite = frames[0];
        spriteRenderer.color = color;
        FitToHitbox();

        elapsed = 0f;
        isPlaying = true;
        spriteRenderer.enabled = true;
    }

    private void Stop()
    {
        isPlaying = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
    }

    // 히트박스 바닥면(xz)에 맞춰 위치·회전·크기를 설정
    // 스프라이트는 XY 평면이므로 X축으로 90도 눕혀 스프라이트의 위쪽이 히트박스의 앞(z)을 향하게 함
    private void FitToHitbox()
    {
        Transform hitboxTransform = hitbox.transform;

        Vector3 position = hitboxTransform.TransformPoint(hitbox.center);
        position.y = transform.parent != null ? transform.parent.position.y + heightOffset : position.y;
        transform.SetPositionAndRotation(position, hitboxTransform.rotation * Quaternion.Euler(90f, 0f, 0f));

        // 월드 기준 크기를 스프라이트 크기와 부모 스케일로 나눠 로컬 스케일로 변환 (플레이어 스케일 반영, 부모 스케일은 균일하다고 가정)
        Vector3 worldSize = Vector3.Scale(hitbox.size, hitboxTransform.lossyScale);
        Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
        float parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
        transform.localScale = new Vector3(
            worldSize.x * sizeMultiplier.x / spriteSize.x / parentScale,
            worldSize.z * sizeMultiplier.y / spriteSize.y / parentScale,
            1f);
    }
}
