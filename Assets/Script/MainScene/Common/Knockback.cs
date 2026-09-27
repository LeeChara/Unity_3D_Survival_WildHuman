using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Knockback : MonoBehaviour
{
    [SerializeField] private float deceleration = 50f; // 클수록 빨리 멈춤 (넉백 거리와 무관하게 감속도 고정)
    [SerializeField, Range(0f, 1f)] private float resistance = 0f; // 1이면 넉백을 완전히 무시

    private Rigidbody rb;
    private Vector3 direction;
    private float currentSpeed;

    public bool IsActive { get; private set; }

    // 회피, 슈퍼아머 등 넉백을 무시해야 하는 조건을 외부에서 지정
    public Func<bool> IsImmune;

    // 지정하면 인스펙터의 resistance 대신 사용 (몬스터는 MonsterData 값을 실시간으로 참조)
    public Func<float> ResistanceProvider;

    public event Action Started;
    public event Action Ended;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Apply(Vector3 direction, float distance)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        if (IsImmune != null && IsImmune()) return;

        float currentResistance = ResistanceProvider != null ? Mathf.Clamp01(ResistanceProvider()) : resistance;
        float finalDistance = distance * (1f - currentResistance);
        if (finalDistance <= 0f) return;

        this.direction = direction.normalized;
        // 등감속 운동: 이동거리 = v0² / (2 * 감속도)
        currentSpeed = Mathf.Sqrt(2f * deceleration * finalDistance);
        IsActive = true;

        Started?.Invoke();
    }

    private void FixedUpdate()
    {
        if (!IsActive) return;

        float dt = Time.fixedDeltaTime;
        float nextSpeed = currentSpeed - deceleration * dt;

        // 이번 스텝의 실제 이동거리를 평균 속도로 환산해 목표 거리를 정확히 맞춤
        float stepDistance = nextSpeed > 0f
            ? (currentSpeed + nextSpeed) * 0.5f * dt
            : currentSpeed * currentSpeed / (2f * deceleration);

        Vector3 velocity = direction * (stepDistance / dt);
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;

        currentSpeed = nextSpeed;
        if (currentSpeed <= 0f)
        {
            // 마지막 스텝 이동은 이번 물리 스텝에 반영되고, 다음 스텝부터 원래 이동 로직이 속도를 제어
            IsActive = false;
            Ended?.Invoke();
        }
    }
}
