using UnityEngine;

// 땅에 떨어진 아이템
// 드랍 직후 포물선을 그리며 튀어 나가고, 일정 시간이 지나야 수집기가 끌어갈 수 있음
// 끌려가는 이동은 ItemCollector가 담당하고, 여기서는 상태(데이터·개수·수명·선점)만 관리
public class WorldItem : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;

    public ItemData Data { get; private set; }
    public int Count { get; private set; }
    // 이 아이템을 끌어가고 있는 수집기 (한 아이템을 여러 수집기가 동시에 당기지 않도록 선점)
    public ItemCollector Collector { get; private set; }

    public bool CanBeClaimed => !isPopping && pickupTimer <= 0f && Collector == null;

    private ItemRule rule;
    private float lifetime;
    private float pickupTimer;

    private bool isPopping;
    private float popTime;
    private Vector3 popStart;
    private Vector3 popEnd;

    // pickupDelay를 지정하지 않으면(음수) 규칙의 기본 대기 시간을 사용 (던진 아이템은 더 길게 지정)
    public void Init(ItemData data, int count, Vector3 start, Vector3 landing, ItemRule rule, float pickupDelay = -1f)
    {
        Data = data;
        Count = count;
        Collector = null;
        this.rule = rule;

        spriteRenderer.sprite = data.icon;

        lifetime = rule.groundLifetime;
        pickupTimer = pickupDelay >= 0f ? pickupDelay : rule.pickupDelay;

        isPopping = rule.popDuration > 0f;
        popTime = 0f;
        popStart = start;
        popEnd = landing;
        transform.position = isPopping ? start : landing;
    }

    private void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Despawn();
            return;
        }

        if (pickupTimer > 0f) pickupTimer -= Time.deltaTime;
        if (isPopping) UpdatePop();
    }

    // 시작점→착지점 직선 이동에 높이 곡선 4h·t(1-t)를 더한 포물선
    private void UpdatePop()
    {
        popTime += Time.deltaTime;
        float t = Mathf.Clamp01(popTime / rule.popDuration);

        Vector3 position = Vector3.Lerp(popStart, popEnd, t);
        position.y += 4f * rule.popHeight * t * (1f - t);
        transform.position = position;

        if (t >= 1f) isPopping = false;
    }

    // ItemCollector만 호출 (수집기의 선점 목록과 함께 갱신해야 하므로)
    public void SetCollector(ItemCollector collector)
    {
        Collector = collector;
    }

    // 수신기가 받은 만큼 개수를 줄이고, 다 받았으면 제거
    // 다 못 받았으면(가득 참) 매 프레임 재시도하지 않도록 획득 대기 시간을 다시 적용
    public void Consume(int amount)
    {
        Count -= amount;
        if (Count <= 0)
        {
            Despawn();
            return;
        }

        pickupTimer = rule.pickupDelay;
    }

    private void Despawn()
    {
        if (Collector != null) Collector.Unclaim(this);
        ItemDropper.Instance.Release(this);
    }
}
