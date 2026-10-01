using System;
using System.Collections.Generic;
using UnityEngine;

// 범위 안의 월드 아이템을 끌어당겨 획득
// 플레이어, 상자, 수집 몬스터 등 어디에나 붙여서 사용하고, 받은 아이템은 같은 오브젝트의 IItemReceiver에 넘김
// 컴포넌트를 끄면 끌어가던 아이템을 모두 놓아줌 (몬스터 AI가 줍기 여부를 제어할 때 사용)
public class ItemCollector : MonoBehaviour
{
    [SerializeField] private LayerMask itemLayer;
    [Tooltip("아이템을 끌어당기기 시작하는 반경")]
    [SerializeField] private float pullRadius = 3f;
    [Tooltip("끌려오던 아이템을 놓아주는 반경. 끌어당김 반경보다 넓어야 경계에서 선점/해제가 반복되지 않음")]
    [SerializeField] private float releaseRadius = 6f;
    [SerializeField] private float pullSpeed = 10f;
    [Tooltip("이 거리 안으로 들어오면 획득")]
    [SerializeField] private float collectDistance = 0.3f;

    // 획득 시점을 UI·사운드 등 외부 시스템에 알림 (아이템, 실제로 받은 개수)
    public event Action<ItemData, int> Collected;

    private IItemReceiver receiver;
    private readonly List<WorldItem> claimed = new();
    private readonly Collider[] hitBuffer = new Collider[32];

    private void Awake()
    {
        receiver = GetComponent<IItemReceiver>();
        if (receiver == null)
            Debug.LogWarning($"[ItemCollector] 같은 오브젝트에 IItemReceiver가 없음: {name}", this);
    }

    private void OnDisable()
    {
        foreach (var item in claimed)
        {
            item.SetCollector(null);
        }
        claimed.Clear();
    }

    private void Update()
    {
        ClaimNearbyItems();
        PullClaimedItems();
    }

    private void ClaimNearbyItems()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, pullRadius, hitBuffer,
            itemLayer, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            if (!hitBuffer[i].TryGetComponent(out WorldItem item)) continue;
            if (!item.CanBeClaimed) continue;

            claimed.Add(item);
            item.SetCollector(this);
        }
    }

    // 획득·해제로 목록에서 빠질 수 있으므로 역순으로 순회
    private void PullClaimedItems()
    {
        Vector3 collectPoint = transform.position;
        float releaseRadiusSqr = releaseRadius * releaseRadius;

        for (int i = claimed.Count - 1; i >= 0; i--)
        {
            WorldItem item = claimed[i];
            Vector3 toCollector = collectPoint - item.transform.position;
            float distanceSqr = toCollector.sqrMagnitude;

            if (distanceSqr > releaseRadiusSqr)
            {
                Unclaim(item);
                continue;
            }

            if (distanceSqr <= collectDistance * collectDistance)
            {
                Collect(item);
                continue;
            }

            float distance = Mathf.Sqrt(distanceSqr);
            item.transform.position += toCollector / distance * Mathf.Min(pullSpeed * Time.deltaTime, distance);
        }
    }

    // 선점을 먼저 풀고 개수를 줄여야, 다 받아서 제거될 때 Unclaim이 중복 호출되지 않음
    private void Collect(WorldItem item)
    {
        Unclaim(item);

        int accepted = receiver != null ? receiver.TryAdd(item.Data, item.Count) : 0;
        if (accepted > 0) Collected?.Invoke(item.Data, accepted);

        item.Consume(accepted);
    }

    public void Unclaim(WorldItem item)
    {
        claimed.Remove(item);
        item.SetCollector(null);
    }

    private void OnValidate()
    {
        releaseRadius = Mathf.Max(releaseRadius, pullRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, pullRadius);
    }
}
