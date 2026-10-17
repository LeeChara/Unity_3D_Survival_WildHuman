using UnityEngine;

// 빙판 위를 걷는 생물 (플레이어, 몬스터)
// 이동 코드는 그대로 두고, 물리 프레임마다 다른 스크립트가 속도를 쓴 뒤에 이 컴포넌트가 고쳐 씀:
// 이동 코드가 원하는 속도를 목표로 삼고, 실제 속도는 iceAcceleration만큼만 따라가서 관성으로 미끄러짐
// 몬스터는 Update에서 속도를 쓰므로 물리 프레임 사이에 새로 쓰지 않을 수 있음
// → 지난번에 이 컴포넌트가 쓴 값과 같으면 새 명령이 없던 것으로 보고 이전 목표를 유지
// 넉백·구르기·몬스터 돌진처럼 순간 속도가 중요한 동작은 건너뜀 (끝난 뒤 남은 속도부터 다시 미끄러짐)
[DefaultExecutionOrder(1000)]
public class IceSlider : MonoBehaviour
{
    // 이보다 크게 달라지면 이동 코드가 새로 쓴 속도로 봄 (물리 계산으로 생기는 작은 오차는 무시)
    private const float CommandThreshold = 0.05f;

    [SerializeField] private WaterSetting setting;

    public bool OnIce { get; private set; }

    // true를 돌려주는 동안은 미끄러짐을 적용하지 않음 (소유 스크립트가 지정, 예: 구르기 중)
    public System.Func<bool> Bypass;

    private Rigidbody rb;
    private Knockback knockback;
    private bool sliding;
    // 이동 코드가 원하는 속도 (수평)
    private Vector3 desired;
    // 지난 물리 프레임에 이 컴포넌트가 쓴 속도 (수평)
    private Vector3 written;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        knockback = GetComponent<Knockback>();
    }

    private void OnDisable()
    {
        OnIce = false;
        sliding = false;
    }

    private void FixedUpdate()
    {
        OnIce = HazardQuery.IsInside(transform.position, setting.iceLayers);

        bool bypass = rb.isKinematic
                   || (knockback != null && knockback.IsActive)
                   || (Bypass != null && Bypass());
        if (!OnIce || bypass)
        {
            sliding = false;
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);

        // 빙판에 막 올라섰으면 지금 속도에서 시작
        if (!sliding)
        {
            sliding = true;
            desired = flat;
            written = flat;
            return;
        }

        if ((flat - written).sqrMagnitude > CommandThreshold * CommandThreshold) desired = flat;

        written = Vector3.MoveTowards(written, desired, setting.iceAcceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(written.x, velocity.y, written.z);
    }
}
