using System.Collections;
using UnityEngine;

// 도약: 목표가 멀리 떨어지면 웅크렸다가 목표가 있던 자리로 뛰어올라 내려찍음 (거리를 벌린 플레이어 견제)
// 착지 지점은 뛰기 전에 정하고 원형으로 예고. 공중에 있는 동안은 물리로 움직이지 않음 (다른 것에 막히지 않음)
[CreateAssetMenu(fileName = "Leap", menuName = "Monster/Boss Pattern/Leap")]
public class LeapPattern : BossPattern
{
    [Tooltip("웅크리는 시간 (초)")]
    [Min(0f)] public float crouch = 0.6f;
    [Tooltip("공중에 떠 있는 시간 (초)")]
    [Min(0.05f)] public float airTime = 0.7f;
    [Tooltip("가장 높이 떴을 때 높이 (m)")]
    [Min(0f)] public float height = 3f;
    [Tooltip("최대 도약 거리 (m, 목표가 더 멀면 이 거리까지만)")]
    [Min(0f)] public float maxDistance = 20f;

    [Header("착지")]
    public AttackShape landShape = AttackShape.Circle(4f);
    public AttackData attack = new AttackData { damage = 25, knockbackDistance = 4f, impactStun = 0.8f };

    [Tooltip("착지 후 후딜 (초)")]
    [Min(0f)] public float recover = 1f;

    private void Reset()
    {
        displayName = "도약";
        minRange = 12f;
        maxRange = 26f;
        cooldown = 6f;
        weight = 3f;
    }

    public override IEnumerator Run(BossAI boss)
    {
        Vector3 forward = boss.AimAtTarget();
        Vector3 start = boss.Body.position;
        Vector3 offset = boss.Target.position - start;
        offset.y = 0f;
        offset = Vector3.ClampMagnitude(offset, maxDistance);
        Vector3 landing = start + offset;

        // 웅크리는 동안부터 착지까지 범위가 차오름
        AttackIndicator indicator = boss.ShowIndicator(landShape, landing, forward, crouch + airTime);
        boss.PlayMotion("LeapWindup", BossMotion.Windup);
        yield return new WaitForSeconds(crouch);

        boss.PlayMotion("Leap", BossMotion.Move);
        boss.Body.isKinematic = true;
        Transform visual = boss.VisualRoot;
        Vector3 visualBase = visual != null ? visual.localPosition : Vector3.zero;
        // 시각 루트는 보스 스케일 안에 있으므로 높이를 로컬 단위로 바꿈
        float heightScale = visual != null && visual.parent != null ? 1f / visual.parent.lossyScale.y : 1f;

        float elapsed = 0f;
        while (elapsed < airTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / airTime);
            boss.Body.MovePosition(Vector3.Lerp(start, landing, t));
            if (visual != null) visual.localPosition = visualBase + Vector3.up * (4f * height * t * (1f - t) * heightScale);
            yield return null;
        }

        boss.ResetPose();
        boss.PlayMotion("LeapLand", BossMotion.Attack);
        boss.Strike(landShape, landing, forward, attack);
        boss.ReleaseIndicator(indicator);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.3f, 0.25f);

        boss.PlayMotion("LeapRecover", BossMotion.Recover);
        yield return new WaitForSeconds(recover);
    }
}
