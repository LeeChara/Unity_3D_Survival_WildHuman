using UnityEngine;

// 음식을 먹으면 파츠 스프라이트를 잠깐 회복 색으로 물들이고, 몸 주변에 반짝이를 띄움
// 몸 반짝임은 피격 반짝임(HitFlash)과 같은 파츠를 쓰므로, 피격되면 즉시 중단해 피격 색이 우선하도록 함
// 주변 반짝이는 파츠 색과 겹치지 않으므로 피격과 상관없이 끝까지 재생
public class PlayerEatEffect : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private HitFlashSetting setting;
    [SerializeField] private PlayerItemUser itemUser;
    [SerializeField] private Health health;
    [Tooltip("먹었을 때 색이 바뀌는 파츠 (HitFlash와 같은 파츠)")]
    [SerializeField] private SpriteRenderer[] targets;

    [Header("주변 반짝이")]
    [SerializeField] private Sprite sparkleSprite;
    [Tooltip("반짝이를 붙일 부모 (빌보드·정렬 그룹을 따르도록 파츠의 부모인 Root를 지정)")]
    [SerializeField] private Transform sparkleRoot;
    [SerializeField, Min(0)] private int sparkleCount = 4;
    [Tooltip("반짝이가 나타나는 범위의 중심 (sparkleRoot 기준 로컬 좌표)")]
    [SerializeField] private Vector2 spawnCenter = Vector2.zero;
    [Tooltip("반짝이가 나타나는 범위의 가로·세로 크기")]
    [SerializeField] private Vector2 spawnArea = new Vector2(0.4f, 0.4f);
    [Tooltip("가장 커졌을 때의 크기 배율 (1이면 스프라이트 원래 크기)")]
    [SerializeField] private float sparkleScale = 0.5f;
    [Tooltip("반짝이 하나가 커졌다 사라지기까지의 시간")]
    [SerializeField] private float sparkleDuration = 0.5f;
    [Tooltip("반짝이끼리 나타나는 시간 간격")]
    [SerializeField] private float sparkleInterval = 0.08f;
    [Tooltip("사라질 때까지 위로 떠오르는 거리")]
    [SerializeField] private float riseDistance = 0.1f;

    // 회복 색이 유지되고 남은 시간
    private float timer;

    private SpriteRenderer[] sparkles;
    private Vector3[] sparkleStartPositions;
    // 반짝이별 경과 시간 (음수면 시작 전 대기, sparkleDuration 이상이면 끝)
    private float[] sparkleElapsed;

    private void Awake()
    {
        if (itemUser == null) itemUser = GetComponent<PlayerItemUser>();
        if (health == null) health = GetComponent<Health>();

        CreateSparkles();
    }

    private void OnEnable()
    {
        itemUser.ItemUsed += OnItemUsed;
        health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        itemUser.ItemUsed -= OnItemUsed;
        health.Damaged -= OnDamaged;

        if (timer > 0f) EndFlash();
        HideSparkles();
    }

    private void Update()
    {
        UpdateFlash();
        UpdateSparkles();
    }

    // 반짝이는 중에 다시 먹으면 지속시간만 다시 채우고, 반짝이는 처음부터 다시 재생
    private void OnItemUsed(ItemData item)
    {
        if (item is not FoodItemData) return;

        timer = setting.healDuration;
        SetColor(setting.healColor);

        PlaySparkles();
    }

    // 색 복구는 HitFlash가 하므로 타이머만 끔 (여기서 흰색으로 되돌리면 피격 색이 지워짐)
    private void OnDamaged()
    {
        timer = 0f;
    }

    private void UpdateFlash()
    {
        if (timer <= 0f) return;

        timer -= Time.deltaTime;
        if (timer <= 0f) EndFlash();
    }

    private void EndFlash()
    {
        timer = 0f;
        SetColor(Color.white);
    }

    // SpriteRenderer.color는 원래 색에 곱해지므로 흰색이면 원래 색 그대로
    private void SetColor(Color value)
    {
        if (targets == null) return;

        foreach (SpriteRenderer target in targets)
        {
            if (target != null) target.color = value;
        }
    }

    // 먹을 때마다 생성하지 않도록 미리 만들어 두고 재사용
    // 파츠와 같은 머티리얼·정렬 레이어를 쓰고, 파츠보다 앞에 그려지도록 Order를 가장 높게 설정
    private void CreateSparkles()
    {
        if (sparkleSprite == null || sparkleRoot == null) sparkleCount = 0;

        sparkles = new SpriteRenderer[sparkleCount];
        sparkleStartPositions = new Vector3[sparkleCount];
        sparkleElapsed = new float[sparkleCount];

        SpriteRenderer reference = targets != null && targets.Length > 0 ? targets[0] : null;
        int topOrder = 0;
        if (targets != null)
        {
            foreach (SpriteRenderer target in targets)
            {
                if (target != null) topOrder = Mathf.Max(topOrder, target.sortingOrder);
            }
        }

        for (int i = 0; i < sparkleCount; i++)
        {
            GameObject sparkleObject = new GameObject($"EatSparkle{i}");
            sparkleObject.transform.SetParent(sparkleRoot, false);

            SpriteRenderer sparkle = sparkleObject.AddComponent<SpriteRenderer>();
            sparkle.sprite = sparkleSprite;
            sparkle.sortingOrder = topOrder + 1;
            if (reference != null)
            {
                sparkle.sharedMaterial = reference.sharedMaterial;
                sparkle.sortingLayerID = reference.sortingLayerID;
            }
            sparkle.enabled = false;

            sparkles[i] = sparkle;
            sparkleElapsed[i] = sparkleDuration;
        }
    }

    // 범위 안 무작위 위치에서, 순서대로 sparkleInterval만큼 늦게 시작
    private void PlaySparkles()
    {
        for (int i = 0; i < sparkles.Length; i++)
        {
            Vector2 offset = new Vector2(
                Random.Range(-0.5f, 0.5f) * spawnArea.x,
                Random.Range(-0.5f, 0.5f) * spawnArea.y);
            sparkleStartPositions[i] = spawnCenter + offset;
            sparkleElapsed[i] = -i * sparkleInterval;
        }
    }

    // 크기가 0 → sparkleScale → 0으로 변하면서 위로 떠오름
    private void UpdateSparkles()
    {
        for (int i = 0; i < sparkles.Length; i++)
        {
            if (sparkleElapsed[i] >= sparkleDuration) continue;

            sparkleElapsed[i] += Time.deltaTime;
            SpriteRenderer sparkle = sparkles[i];

            if (sparkleElapsed[i] < 0f || sparkleElapsed[i] >= sparkleDuration)
            {
                sparkle.enabled = false;
                continue;
            }

            float t = sparkleElapsed[i] / sparkleDuration;
            sparkle.transform.localPosition = sparkleStartPositions[i] + Vector3.up * (riseDistance * t);
            sparkle.transform.localScale = Vector3.one * (sparkleScale * Mathf.Sin(t * Mathf.PI));
            sparkle.enabled = true;
        }
    }

    private void HideSparkles()
    {
        for (int i = 0; i < sparkles.Length; i++)
        {
            sparkleElapsed[i] = sparkleDuration;
            sparkles[i].enabled = false;
        }
    }
}
