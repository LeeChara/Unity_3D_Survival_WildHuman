using UnityEngine;

// 거치대에 매달린 종이 화면 평면에서 진자처럼 왕복하고, 일정 간격마다 크게 흔들리다 끝에서 스스로 울림 (사방으로 음파)
// 스스로 울리는 음파는 약하게(autoPower), 플레이어가 강타하면 기존처럼 세기 1로 울리고 종이 크게 흔들림
// 스스로 울리는 것은 플레이어가 가까이(activeRange) 있을 때만 (멀리서 아무도 못 보는 음파가 몬스터를 기절시키지 않도록)
[RequireComponent(typeof(Resonant))]
public class BellSwing : MonoBehaviour
{
    [Tooltip("종이 매달린 회전축 (거치대 고리 위치, 빌보드 평면 안에서 로컬 Z축으로 회전)")]
    [SerializeField] private Transform pivot;

    [Header("흔들림")]
    [Tooltip("평소 흔들림 각도 (도, 한쪽)")]
    [SerializeField, Min(0f)] private float idleAngle = 2f;
    [Tooltip("울릴 때 흔들림 각도 (도, 한쪽)")]
    [SerializeField, Min(0f)] private float ringAngle = 25f;
    [Tooltip("한 번 왕복하는 시간 (초)")]
    [SerializeField, Min(0.1f)] private float period = 2f;
    [Tooltip("울리기 전에 흔들림이 커지는 시간 (초)")]
    [SerializeField, Min(0.01f)] private float buildUpTime = 1.5f;
    [Tooltip("울린 뒤 평소 흔들림으로 잦아드는 시간 (초)")]
    [SerializeField, Min(0.01f)] private float settleTime = 3f;

    [Header("스스로 울림")]
    [Tooltip("스스로 울리는 간격 (초)")]
    [SerializeField, Min(0.5f)] private float ringInterval = 10f;
    [Tooltip("스스로 울릴 때 음파 세기 (1 = 강타와 같음, 0.6이면 9m)")]
    [SerializeField, Range(0f, 1f)] private float autoPower = 0.6f;
    [Tooltip("플레이어가 이 거리(수평) 안에 있을 때만 스스로 울림")]
    [SerializeField, Min(0f)] private float activeRange = 40f;

    private Resonant resonant;
    private Transform player;

    // 진자 위상 (0~1, 한 번 왕복)
    private float phase;
    private float amplitude;
    private float timer;
    // 울린 뒤 잦아드는 중이면 남은 시간
    private float settleLeft;
    // 이번 울림은 스스로 낸 것 (Struck에서 흔들림을 다시 키우지 않도록)
    private bool selfStriking;

    private void Awake()
    {
        resonant = GetComponent<Resonant>();
        amplitude = idleAngle;
        // 여러 종이 같은 박자로 흔들리고 울리지 않도록 어긋나게 시작
        phase = Random.value;
        timer = Random.value * ringInterval;
    }

    private void OnEnable()
    {
        resonant.Struck += OnStruck;
    }

    private void OnDisable()
    {
        resonant.Struck -= OnStruck;
    }

    private void Start()
    {
        var health = FindAnyObjectByType<PlayerHealth>();
        if (health != null) player = health.transform;
    }

    private void Update()
    {
        float previousPhase = phase;
        phase = Mathf.Repeat(phase + Time.deltaTime / period, 1f);

        bool active = IsPlayerNear();
        if (active) timer += Time.deltaTime;

        float target = idleAngle;
        if (settleLeft > 0f)
        {
            // 울린 직후에는 크게 흔들리다가 천천히 평소로
            settleLeft -= Time.deltaTime;
            target = Mathf.Lerp(idleAngle, ringAngle, Mathf.Clamp01(settleLeft / settleTime));
        }
        else if (active && timer >= ringInterval - buildUpTime)
        {
            target = ringAngle;
        }

        float rate = Mathf.Abs(ringAngle - idleAngle) / buildUpTime;
        amplitude = Mathf.MoveTowards(amplitude, target, rate * Time.deltaTime);

        // 흔들림이 다 커진 뒤 끝(위상 0.25·0.75)에 닿는 순간 울림
        if (active && timer >= ringInterval && Mathf.Approximately(amplitude, ringAngle) && PassedPeak(previousPhase, phase))
        {
            timer = 0f;
            selfStriking = true;
            if (resonant.StrikeSelf(autoPower)) settleLeft = settleTime;
            selfStriking = false;
        }

        if (pivot != null) pivot.localRotation = Quaternion.Euler(0f, 0f, amplitude * Mathf.Sin(phase * Mathf.PI * 2f));
    }

    // 플레이어가 강타하거나 음파가 닿아 울리면 크게 흔들림
    private void OnStruck()
    {
        if (selfStriking) return;
        amplitude = ringAngle;
        settleLeft = settleTime;
        timer = 0f;
    }

    private bool IsPlayerNear()
    {
        if (player == null) return false;
        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= activeRange * activeRange;
    }

    private static bool PassedPeak(float from, float to)
    {
        return Crossed(from, to, 0.25f) || Crossed(from, to, 0.75f);
    }

    // 위상이 한 바퀴를 넘어가는 경우도 고려
    private static bool Crossed(float from, float to, float mark)
    {
        return from <= to ? from < mark && to >= mark : from < mark || to >= mark;
    }
}
