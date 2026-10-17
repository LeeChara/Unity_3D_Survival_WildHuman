using UnityEngine;

// 켜져 있는 동안 일정 간격으로 파티클을 뿌림 (예: 모닥불 불씨, 제련소 굴뚝 연기)
// 항상 켜 두거나, 다른 컴포넌트가 enabled로 켜고 끔 (예: Smelter는 재련 중일 때만)
// activeWhen을 지정하면 그 컴포넌트가 켜져 있을 때만 뿌림 (예: 모닥불은 FireSource가 켜져 있을 때)
// 뿌리는 범위는 이 오브젝트의 그림 범위 (point를 지정하면 그 지점)
public class LoopingEffect : MonoBehaviour
{
    [SerializeField] private EffectData effect;
    [Tooltip("뿌리는 간격 (초, 매번 ±30% 흔듦)")]
    [SerializeField, Min(0.05f)] private float interval = 0.5f;
    [Tooltip("비워두면 그림 범위 전체에서, 지정하면 이 지점에서 뿌림 (예: 굴뚝 끝)")]
    [SerializeField] private Transform point;
    [Tooltip("이 컴포넌트가 켜져 있을 때만 뿌림 (비워두면 항상)")]
    [SerializeField] private Behaviour activeWhen;

    private float timer;
    // 그림 범위 (처음 한 번 잼, 위치는 이 오브젝트 기준)
    private bool measured;
    private Vector3 coverOffset;
    private Vector3 coverSize;

    private void OnEnable()
    {
        timer = Random.value * interval;
    }

    private void Update()
    {
        if (effect == null || (activeWhen != null && !activeWhen.enabled)) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = interval * Random.Range(0.7f, 1.3f);

        if (point != null)
        {
            Effects.Play(effect, new Bounds(point.position, Vector3.zero), transform);
            return;
        }

        if (!measured)
        {
            measured = true;
            if (BurnEffect.TryMeasure(transform, null, null, out Bounds cover))
            {
                coverOffset = cover.center - transform.position;
                coverSize = cover.size;
            }
        }
        Effects.Play(effect, new Bounds(transform.position + coverOffset, coverSize), transform);
    }
}
