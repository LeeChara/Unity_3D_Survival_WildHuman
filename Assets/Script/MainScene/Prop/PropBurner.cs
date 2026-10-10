using UnityEngine;

// 가연성 자원(PropData.flammable)이 화염 속성 공격에 맞으면 불이 붙음 (예: 랜턴 × 나무)
// 타는 동안 불꽃·빛을 켜고 점점 그을리며, burnDuration이 지나면 다 타서 burnDropTable을 떨어뜨리고 사라짐
// 타는 중에 다른 공격으로 쓰러지면 원래 드랍을 그대로 떨어뜨리고 불도 꺼짐
// 타는 상태는 저장하지 않음 (청크 언로드·불러오기 시 원래 나무로 돌아옴)
[RequireComponent(typeof(PropHealth))]
public class PropBurner : MonoBehaviour
{
    [Header("표시")]
    [Tooltip("타는 동안 켜지는 불꽃 스프라이트")]
    [SerializeField] private SpriteRenderer flame;
    [Tooltip("타는 동안 켜지는 광원 (처음에는 꺼 둠)")]
    [SerializeField] private LightSource fireLight;
    [Tooltip("그을림 색을 적용할 피격 반짝임 (원래 색 대신 이 색으로 돌아가도록)")]
    [SerializeField] private HitFlash hitFlash;
    [Tooltip("다 탔을 때의 색 (타는 동안 흰색에서 이 색으로 바뀜)")]
    [SerializeField] private Color charredColor = new Color(0.3f, 0.25f, 0.22f);

    [Header("불꽃 애니메이션")]
    [Tooltip("타는 동안 반복 재생할 불꽃 프레임")]
    [SerializeField] private Sprite[] flameFrames;
    [Tooltip("초당 프레임 수")]
    [SerializeField, Min(0.1f)] private float frameRate = 8f;
    [Tooltip("불꽃 색 (알파로 투명도 조절)")]
    [SerializeField] private Color flameColor = new Color(1f, 1f, 1f, 0.85f);

    private PropHealth health;
    private float elapsed;

    public bool IsBurning { get; private set; }

    private void Awake()
    {
        health = GetComponent<PropHealth>();
        flame.color = flameColor;
    }

    // 풀에서 재사용될 때마다 불이 꺼진 원래 상태로 시작
    private void OnEnable()
    {
        health.Hit += OnHit;
        health.Died += Extinguish;
        Extinguish();
    }

    private void OnDisable()
    {
        health.Hit -= OnHit;
        health.Died -= Extinguish;
    }

    private void Update()
    {
        if (!IsBurning) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / health.Data.burnDuration);
        if (hitFlash != null) hitFlash.SetBaseColor(Color.Lerp(Color.white, charredColor, t));

        if (flameFrames != null && flameFrames.Length > 0)
        {
            flame.sprite = flameFrames[Mathf.FloorToInt(elapsed * frameRate) % flameFrames.Length];
        }

        if (t >= 1f) health.BurnOut();
    }

    private void OnHit(AttackData attack)
    {
        if (IsBurning || health.IsDead) return;
        if (attack.element != ElementType.Fire || !health.Data.flammable) return;

        IsBurning = true;
        elapsed = 0f;
        flame.enabled = true;
        if (fireLight != null) fireLight.enabled = true;
    }

    private void Extinguish()
    {
        IsBurning = false;
        elapsed = 0f;
        flame.enabled = false;
        if (fireLight != null) fireLight.enabled = false;
        // 쓰러진 직후에는 HitFlash가 색을 바로 바꾸지 않고, 풀에 반환될 때(OnDisable) 흰색으로 돌아감
        if (hitFlash != null) hitFlash.SetBaseColor(Color.white);
    }
}
