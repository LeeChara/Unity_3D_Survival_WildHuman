using UnityEngine;

// 바닥에서 타오르는 불길 하나 (보스의 불의 고리·돌진 화염 장판)
// 불꽃 프레임을 돌려 그리고, 수명이 있으면 끝날 무렵 옅어지다 사라짐 (수명 0이면 Extinguish로 끌 때까지)
// 피해·넉백은 FireHazard(트리거), 주변 물체에 불을 옮기는 것은 FireSource가 담당
public class GroundFlame : MonoBehaviour
{
    [SerializeField] private SpriteRenderer flame;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(0.1f)] private float frameRate = 8f;
    [Tooltip("사라지기 전 옅어지는 시간 (초)")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.6f;
    [SerializeField] private FireHazard hazard;
    [SerializeField] private LightSource fireLight;

    private float lifetime;
    private float elapsed;
    private float phase;
    private bool extinguishing;
    private float fadeLeft;
    private Color baseColor;

    public FireHazard Hazard => hazard;
    public bool HasLight => fireLight != null && fireLight.gameObject.activeSelf;

    // lifetime: 0이면 끌 때까지 계속 탐. owner: 피해를 주지 않는 대상
    public static GroundFlame Spawn(GroundFlame prefab, Vector3 position, float lifetime, Transform owner, bool withLight)
    {
        GroundFlame instance = Instantiate(prefab, position, Quaternion.identity);
        instance.lifetime = lifetime;
        if (instance.hazard != null) instance.hazard.Owner = owner;
        if (instance.fireLight != null) instance.fireLight.gameObject.SetActive(withLight);
        return instance;
    }

    private void Awake()
    {
        baseColor = flame.color;
        phase = Random.value * 10f;
        flame.flipX = Random.value < 0.5f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (frames != null && frames.Length > 0) flame.sprite = frames[Mathf.FloorToInt((elapsed + phase) * frameRate) % frames.Length];

        if (!extinguishing && lifetime > 0f && elapsed >= lifetime) Extinguish();
        if (!extinguishing) return;

        fadeLeft -= Time.deltaTime;
        float alpha = Mathf.Clamp01(fadeLeft / fadeDuration);
        flame.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
        if (fadeLeft <= 0f) Destroy(gameObject);
    }

    // 피해를 멈추고 옅어지며 사라짐
    public void Extinguish()
    {
        if (extinguishing) return;

        extinguishing = true;
        fadeLeft = fadeDuration;
        // FireHazard는 컴포넌트를 꺼도 트리거 메시지를 받으므로 오브젝트째 끔
        if (hazard != null) hazard.gameObject.SetActive(false);
        if (fireLight != null) fireLight.gameObject.SetActive(false);
        if (TryGetComponent(out SfxLoop loop)) loop.Stop();
    }
}
