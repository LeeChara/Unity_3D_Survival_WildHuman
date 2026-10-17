using UnityEngine;

// 플레이어 파티클: 발걸음 먼지(바이옴별), 물 위 걸음·물에 들어갈 때 첨벙, 빙판 얼음 가루, 아이템 획득 반짝
// 각 기능의 상태를 읽어서 파티클만 뿌림 (파티클을 뺄 때 이 컴포넌트만 지우면 됨, PlayerSfx와 같은 방식)
// 바이옴별 발걸음 먼지는 BiomeData.footstepEffect
public class PlayerParticles : MonoBehaviour
{
    [Header("발걸음")]
    [Tooltip("먼지를 일으키는 걸음 거리 (m)")]
    [SerializeField, Min(0.1f)] private float stride = 1.1f;
    [Tooltip("물 위를 걸을 때 (비워두면 없음)")]
    [SerializeField] private EffectData waterStepEffect;
    [Tooltip("빙판 위를 걷거나 미끄러질 때 (비워두면 없음)")]
    [SerializeField] private EffectData iceStepEffect;

    [Header("물")]
    [Tooltip("물에 들어가는 순간")]
    [SerializeField] private EffectData waterEnterEffect;

    [Header("아이템")]
    [Tooltip("아이템을 주울 때 몸 주변 반짝 (몸을 따라감)")]
    [SerializeField] private EffectData pickupEffect;

    private Health health;
    private PlayerState playerState;
    private ItemCollector collector;
    private WaterWader waterWader;
    private IceSlider iceSlider;

    private Vector3 lastPosition;
    private float travelled;
    private bool wasInWater;

    private void Awake()
    {
        health = GetComponent<Health>();
        playerState = GetComponentInChildren<PlayerState>();
        collector = GetComponentInChildren<ItemCollector>();
        waterWader = GetComponent<WaterWader>();
        iceSlider = GetComponent<IceSlider>();
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
        travelled = 0f;
        wasInWater = waterWader != null && waterWader.InWater;
        if (collector != null) collector.Collected += OnCollected;
    }

    private void OnDisable()
    {
        if (collector != null) collector.Collected -= OnCollected;
    }

    private void Update()
    {
        UpdateWaterEnter();
        UpdateSteps();
    }

    private void UpdateWaterEnter()
    {
        bool inWater = waterWader != null && waterWader.InWater;
        if (inWater && !wasInWater && (health == null || !health.IsDead)) Effects.Play(waterEnterEffect, Foot(transform.position));
        wasInWater = inWater;
    }

    // 걸은 거리마다 발밑에 (PlayerSfx 발소리와 같은 방식). 빙판에서는 구르기·넉백으로 미끄러질 때도 뿌림
    private void UpdateSteps()
    {
        Vector3 position = transform.position;
        Vector3 delta = position - lastPosition;
        delta.y = 0f;
        lastPosition = position;

        float distance = delta.magnitude;
        // 순간이동(부활, 세이브 복원)은 걸음으로 치지 않음
        if (distance <= 0.0001f || distance > stride * 4f) return;
        if (health != null && health.IsDead) return;

        bool onIce = iceSlider != null && iceSlider.OnIce;
        bool normal = playerState == null || playerState.CurrentState == PlayerActionState.Normal;
        if (!normal && !onIce) return;

        travelled += distance;
        if (travelled < stride) return;
        travelled -= stride;

        Effects.Play(StepEffect(position, onIce), Foot(position));
    }

    // 플레이어 기준점은 몸 중간 높이라서 지면(높이 0)으로 내림
    private static Vector3 Foot(Vector3 position) => new Vector3(position.x, 0f, position.z);

    private EffectData StepEffect(Vector3 position, bool onIce)
    {
        if (waterWader != null && waterWader.InWater) return waterStepEffect;
        if (onIce) return iceStepEffect;

        WorldState world = WorldState.Instance;
        BiomeData biome = world != null && world.IsInside(position) ? world.GetBiome(world.WorldToChunk(position)) : null;
        return biome != null ? biome.footstepEffect : null;
    }

    private void OnCollected(ItemData item, int count)
    {
        Effects.PlayOn(pickupEffect, transform, null, true);
    }
}
