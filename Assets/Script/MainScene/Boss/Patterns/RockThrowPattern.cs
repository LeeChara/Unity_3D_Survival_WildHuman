using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// 바위 던지기 (2페이즈): 땅에서 큰 바위를 뽑아 목표가 있던 자리로 던짐
// 착지 지점은 뽑기 시작할 때 정하고, 뽑는 동안부터 날아가는 동안까지 원형 범위가 차오름
// 떨어진 바위는 장애물로 남음 (캐면 돌, 보스가 돌진으로 부딪히면 기절하고 바위는 부서짐)
[CreateAssetMenu(fileName = "RockThrow", menuName = "Monster/Boss Pattern/Rock Throw")]
public class RockThrowPattern : BossPattern
{
    [Tooltip("바위를 뽑는 시간 (초)")]
    [Min(0f)] public float pull = 1f;
    [Tooltip("바위가 날아가는 시간 (초)")]
    [Min(0.05f)] public float flightTime = 0.8f;
    [Tooltip("가장 높이 떴을 때 높이 (m)")]
    [Min(0f)] public float height = 5f;
    [Tooltip("손에 든 바위의 높이 (m, 던지기 시작하는 높이)")]
    [Min(0f)] public float holdHeight = 3f;

    [Header("착지")]
    public AttackShape landShape = AttackShape.Circle(3.5f);
    public AttackData attack = new AttackData { damage = 35, knockbackDistance = 4f, impactStun = 0.8f };
    [Tooltip("바닥에 남는 바위")]
    public BossRock rockPrefab;
    [Tooltip("동시에 남아 있을 수 있는 바위 수")]
    [Min(1)] public int maxRocks = 3;

    [Tooltip("바위가 떨어질 때 효과음 (뽑을 때 windupCue, 던질 때 attackCue)")]
    public SfxCue landCue;

    [Tooltip("던진 뒤 후딜 (초)")]
    [Min(0f)] public float recover = 0.8f;

    private void Reset()
    {
        displayName = "바위 던지기";
        usableInPhase1 = false;
        minRange = 6f;
        maxRange = 20f;
        cooldown = 7f;
        weight = 2f;
    }

    public override IEnumerator Run(BossAI boss)
    {
        Vector3 forward = boss.AimAtTarget();
        Vector3 landing = boss.Target.position;
        landing.y = 0f;

        AttackIndicator indicator = boss.ShowIndicator(landShape, landing, forward, pull + flightTime);
        boss.PlayMotion("RockPull", BossMotion.Windup);
        boss.PlaySfx(windupCue);
        yield return new WaitForSeconds(pull);

        boss.PlayMotion("RockThrow", BossMotion.Attack);
        boss.PlaySfx(attackCue);
        Vector3 start = boss.transform.position;
        start.y = 0f;
        Transform rock = CreateFlyingRock(start);

        float elapsed = 0f;
        while (elapsed < flightTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightTime);
            // 바닥 위치는 직선으로, 높이는 들고 있던 높이에서 포물선을 그리며 0까지
            float y = Mathf.Lerp(holdHeight, 0f, t) + 4f * height * t * (1f - t);
            if (rock != null) SetFlyingRock(rock, Vector3.Lerp(start, landing, t), y);
            yield return null;
        }

        if (rock != null) Destroy(rock.gameObject);
        boss.Strike(landShape, landing, forward, attack);
        boss.PlaySfx(landCue, landing);
        boss.ReleaseIndicator(indicator);
        if (rockPrefab != null) BossRock.Spawn(rockPrefab, landing, maxRocks);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.25f, 0.2f);

        boss.PlayMotion("RockRecover", BossMotion.Recover);
        yield return new WaitForSeconds(recover);
    }

    // 날아가는 바위 그림 (바닥 위치로 깊이 정렬하고, 그림만 높이만큼 띄움)
    private Transform CreateFlyingRock(Vector3 position)
    {
        if (rockPrefab == null || !rockPrefab.TryGetComponent(out SpriteRenderer source)) return null;

        var root = new GameObject("FlyingRock");
        root.AddComponent<SortingGroup>();
        root.AddComponent<DepthSorter>();
        root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 45f, 0f));

        var visual = new GameObject("Visual").AddComponent<SpriteRenderer>();
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = rockPrefab.transform.localScale;
        visual.sprite = source.sprite;
        visual.sharedMaterial = source.sharedMaterial;
        return root.transform;
    }

    private static void SetFlyingRock(Transform rock, Vector3 ground, float height)
    {
        rock.position = ground;
        rock.GetChild(0).localPosition = Vector3.up * height;
    }
}
