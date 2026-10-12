using UnityEngine;

// 기절하는 동안 머리 위에 기절 이펙트를 띄움 (플레이어, 몬스터 공용)
// 위치는 이펙트를 띄우는 순간 보이는 몸체 스프라이트의 윗변 기준
public class StunIndicator : MonoBehaviour
{
    [SerializeField] private GameObject effectPrefab;
    [Tooltip("몸체 스프라이트 윗변에서 띄울 높이 (m)")]
    [SerializeField] private float headroom = 0.15f;
    [Tooltip("부모 SortingGroup 안에서의 그리기 순서 (몸체 파츠보다 앞)")]
    [SerializeField] private int sortingOrder = 500;

    private IStunnable stunnable;
    private Health health;
    private GameObject effect;

    private void Awake()
    {
        stunnable = GetComponent<IStunnable>();
        health = GetComponent<Health>();
    }

    private void OnDisable()
    {
        Hide();
    }

    private void LateUpdate()
    {
        bool stunned = stunnable != null && stunnable.IsStunned && (health == null || !health.IsDead);
        if (stunned && effect == null) Show();
        else if (!stunned && effect != null) Hide();
    }

    private void Show()
    {
        if (effectPrefab == null) return;

        float top = BodyTop();
        effect = Instantiate(effectPrefab, transform);
        effect.transform.position = new Vector3(transform.position.x, top + headroom, transform.position.z);

        // 대상 루트의 스케일(7~10배)과 상관없이 같은 크기로 보이도록 되돌림
        Vector3 scale = transform.lossyScale;
        effect.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);

        foreach (SpriteRenderer r in effect.GetComponentsInChildren<SpriteRenderer>()) r.sortingOrder = sortingOrder;
    }

    private void Hide()
    {
        if (effect != null) Destroy(effect);
        effect = null;
    }

    // 상태 아이콘(순서 100 이상)·그림자는 제외한 보이는 몸체 스프라이트의 가장 높은 지점
    private float BodyTop()
    {
        float top = transform.position.y;
        bool found = false;
        foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>())
        {
            if (!r.enabled || r.sprite == null || r.sortingOrder >= 100) continue;
            if (r.sortingLayerName == ShadowSetting.SortingLayerName) continue;

            top = found ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y;
            found = true;
        }
        return top;
    }
}
