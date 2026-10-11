using System.Collections.Generic;
using UnityEngine;

// 화약통: 화염 속성 공격에 맞으면 도화선이 타들어 가다가 폭발함 (예: 랜턴 × 화약통)
// 폭발은 반경 안의 모든 체력(플레이어 포함)에 곡괭이·화염 공격을 줌 → 광맥을 부수고 나무에 불을 붙임
// 폭발에 휘말린 다른 화약통은 짧은 도화선으로 바로 이어서 터짐 (연쇄 폭발)
// 점화된 화약통은 부서져도 재료를 돌려주지 않고 그 자리에서 터짐
// 도화선 상태는 저장하지 않음 (불러오면 꺼진 화약통으로 돌아옴)
[RequireComponent(typeof(StructureHealth))]
public class ExplosiveKeg : MonoBehaviour
{
    [Header("도화선")]
    [Tooltip("화염 공격으로 점화된 뒤 터질 때까지의 시간 (초)")]
    [SerializeField, Min(0f)] private float fuseTime = 3f;
    [Tooltip("다른 폭발에 휘말렸을 때 터질 때까지의 시간 (초)")]
    [SerializeField, Min(0f)] private float chainFuseTime = 0.25f;

    [Header("폭발")]
    [SerializeField, Min(0f)] private float radius = 4f;
    [Tooltip("폭발 공격 (곡괭이 티어로 광맥을 부수고, 화염 속성으로 나무·화약통에 불을 붙임)")]
    [SerializeField] private AttackData blast = new AttackData
    {
        damage = 40,
        knockbackDistance = 3f,
        toolType = ToolType.Pickaxe,
        toolTier = 4,
        element = ElementType.Fire
    };
    [Tooltip("폭발이 닿는 레이어 (플레이어, 몬스터, 자원, 설치물)")]
    [SerializeField] private LayerMask targetLayers;
    [Tooltip("폭발 위치에 생성할 이펙트 (비우면 생략)")]
    [SerializeField] private GameObject explosionEffectPrefab;

    [Header("표시")]
    [Tooltip("타는 동안 켜지는 도화선 불꽃")]
    [SerializeField] private SpriteRenderer spark;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField, Min(0.1f)] private float sparkFrameRate = 12f;
    [Tooltip("타는 동안 켜지는 광원 (처음에는 꺼 둠)")]
    [SerializeField] private LightSource fuseLight;
    [Tooltip("깜빡임 색을 적용할 피격 반짝임")]
    [SerializeField] private HitFlash hitFlash;
    [SerializeField] private Color blinkColor = new Color(1f, 0.45f, 0.35f);
    [Tooltip("도화선 시작·끝의 초당 깜빡임 횟수 (터질 때가 가까울수록 빨라짐)")]
    [SerializeField] private Vector2 blinkRate = new Vector2(2f, 10f);

    private static readonly Collider[] overlapBuffer = new Collider[64];

    private StructureHealth health;
    private ItemDropOnDeath dropOnDeath;
    private float fuseLeft;
    private float fuseLength;
    private float elapsed;
    private bool exploded;

    public bool IsLit { get; private set; }

    private void Awake()
    {
        health = GetComponent<StructureHealth>();
        dropOnDeath = GetComponent<ItemDropOnDeath>();
        spark.enabled = false;
        if (fuseLight != null) fuseLight.enabled = false;
    }

    private void OnEnable()
    {
        health.Hit += OnHit;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Hit -= OnHit;
        health.Died -= OnDied;
    }

    private void Update()
    {
        if (!IsLit || exploded) return;

        elapsed += Time.deltaTime;
        fuseLeft -= Time.deltaTime;

        if (sparkFrames != null && sparkFrames.Length > 0)
        {
            spark.sprite = sparkFrames[Mathf.FloorToInt(elapsed * sparkFrameRate) % sparkFrames.Length];
        }

        if (hitFlash != null)
        {
            float progress = fuseLength > 0f ? 1f - fuseLeft / fuseLength : 1f;
            float rate = Mathf.Lerp(blinkRate.x, blinkRate.y, progress);
            hitFlash.SetBaseColor(Mathf.Repeat(elapsed * rate, 1f) < 0.5f ? blinkColor : Color.white);
        }

        if (fuseLeft <= 0f) health.Kill();
    }

    private void OnHit(AttackData attack)
    {
        if (attack.element != ElementType.Fire) return;
        Ignite(fuseTime);
    }

    // 이미 타는 중이면 남은 시간이 더 짧은 쪽을 따름
    public void Ignite(float fuse)
    {
        if (exploded || health.IsDead) return;

        if (IsLit)
        {
            fuseLeft = Mathf.Min(fuseLeft, fuse);
            return;
        }

        IsLit = true;
        fuseLeft = fuse;
        fuseLength = fuse;
        elapsed = 0f;
        spark.enabled = true;
        if (fuseLight != null) fuseLight.enabled = true;
        // 불붙은 화약통은 부서져도 재료를 돌려주지 않음
        if (dropOnDeath != null) dropOnDeath.SetDropTable(null);
    }

    // 도화선이 다 타서 Kill되었거나, 타는 중에 부서진 경우 그 자리에서 폭발
    private void OnDied()
    {
        if (IsLit) Explode();
    }

    private void Explode()
    {
        if (exploded) return;
        exploded = true;

        Vector3 center = transform.position;
        if (explosionEffectPrefab != null) Instantiate(explosionEffectPrefab, center, Quaternion.identity);

        int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, targetLayers, QueryTriggerInteraction.Ignore);
        var hitTargets = new HashSet<Health>();
        for (int i = 0; i < count; i++)
        {
            // 설치물처럼 콜라이더가 자식에 있는 경우 루트의 체력을 찾음
            Health target = overlapBuffer[i].GetComponentInParent<Health>();
            if (target == null || target == health || target.IsDead || !hitTargets.Add(target)) continue;

            // 다른 화약통은 부수지 않고 짧은 도화선으로 이어서 터뜨림
            if (target.TryGetComponent(out ExplosiveKeg keg))
            {
                keg.Ignite(chainFuseTime);
                continue;
            }

            target.TakeHit(blast);

            if (target.TryGetComponent(out Knockback knockback))
            {
                knockback.Apply(target.transform.position - center, blast.knockbackDistance);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
