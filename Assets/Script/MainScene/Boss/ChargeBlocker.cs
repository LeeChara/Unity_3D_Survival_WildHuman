using Newtonsoft.Json.Linq;
using UnityEngine;

// 보스 돌진에 부딪혔을 때의 반응 (기둥·울타리 등에 붙임)
// 막는 물체(stunDuration > 0)는 돌진을 멈추고 보스를 기절시킴. 막지 않는 물체는 부서지고 돌진은 계속됨
// 이 컴포넌트가 없는 자원·구조물·월드 경계는 돌진만 멈추고 기절은 없음
// 부딪힌 횟수는 저장됨 (둥지 돌기둥처럼 무너진 뒤 다시 세울 수 있는 것은 파괴하지 않고 숨김)
public class ChargeBlocker : MonoBehaviour, ISaveable
{
    [Tooltip("부딪힌 보스의 기절 시간 (초, 0이면 막지 못하고 부서짐)")]
    [SerializeField, Min(0f)] private float stunDuration = 3f;
    [Tooltip("부딪힐 때마다 자신이 받는 피해 (체력이 있는 설치물)")]
    [SerializeField, Min(0)] private int damage = 0;
    [Tooltip("이 횟수만큼 부딪히면 부서짐 (0이면 횟수로는 부서지지 않음, 파괴 불가 구조물도 부서짐)")]
    [SerializeField, Min(0)] private int hitsToBreak = 0;
    [Tooltip("부서질 때 파괴하지 않고 숨김 (다시 세울 수 있음, 예: 보스를 다시 부르면 복구되는 둥지 돌기둥)")]
    [SerializeField] private bool hideOnBreak;

    private int hits;

    public bool Stops => stunDuration > 0f;
    public float StunDuration => stunDuration;
    public bool IsBroken => hitsToBreak > 0 && hits >= hitsToBreak;

    // 보스가 부딪힌 순간 한 번 호출
    public void OnCharged(Vector3 from)
    {
        // 금속이면 강타처럼 울림 (음파는 부딪힌 반대편으로)
        Resonant resonant = GetComponentInParent<Resonant>();
        if (resonant != null) resonant.OnAttackHit(from, true);

        if (!Stops)
        {
            Break();
            return;
        }

        Health health = GetComponentInParent<Health>();
        if (damage > 0 && health != null) health.TakeDamage(damage);

        hits++;
        if (IsBroken) Break();
    }

    private void Break()
    {
        if (hideOnBreak)
        {
            gameObject.SetActive(false);
            return;
        }

        Health health = GetComponentInParent<Health>();
        if (health != null) health.Kill();
        else Destroy(gameObject);
    }

    // 다시 세움 (부딪힌 횟수 초기화)
    public void Restore()
    {
        hits = 0;
        gameObject.SetActive(true);
    }

    #region 저장

    public string SaveKey => "charge";

    public JToken Save() => hits > 0 ? new JObject { ["hits"] = hits } : null;

    public void Load(JToken data)
    {
        hits = data.Value<int>("hits");
        if (hideOnBreak && IsBroken) gameObject.SetActive(false);
    }

    #endregion
}
