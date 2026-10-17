using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

// 보스 둥지 (월드 구조물 루트에 붙음): 둥지를 지키는 보스를 만들고 상태를 저장
// 처음 생긴 둥지에는 보스가 있음. 쓰러뜨리면 비고, 제단에 공물을 바치면 다시 나타남 (돌기둥 복구·던진 바위 정리)
// 보스는 둥지의 자식으로 만들어서 청크가 언로드되면 함께 사라지고, 다시 로드되면 저장된 체력으로 돌아옴
// 저장: 보스가 있는지, 체력, 쓰러뜨린 횟수 (돌기둥 상태는 각 기둥이 부품으로 따로 저장)
public class BossNest : MonoBehaviour, ISaveable
{
    [SerializeField] private BossAI bossPrefab;
    [Tooltip("보스가 나타나는 자리 (비우면 둥지 중심)")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("입구 쪽을 가리키는 자리 (둥지 중심에서 이쪽 방향이 입구, 불의 고리가 비는 곳)")]
    [SerializeField] private Transform entrance;

    // 어느 둥지든 보스를 쓰러뜨린 시점 (데모 클리어 등)
    public static event Action<BossNest> BossDefeated;

    private bool bossAlive = true;
    // 저장된 체력 (0 이하면 가득)
    private float savedHealth;
    private int defeatedCount;
    private BossAI boss;

    public bool HasBoss => bossAlive;
    public int DefeatedCount => defeatedCount;
    public BossAI Boss => boss;
    public BossData BossData => bossPrefab != null ? bossPrefab.Data as BossData : null;

    // 저장된 상태는 생성 직후(Start 전)에 불러와지므로 Start에서 보스를 만듦
    private void Start()
    {
        if (bossAlive) SpawnBoss();
    }

    private void SpawnBoss()
    {
        if (bossPrefab == null) return;

        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
        boss = Instantiate(bossPrefab, position, Quaternion.identity, transform);

        Vector3 entranceDirection = entrance != null ? entrance.position - transform.position : -transform.forward;
        boss.SetHome(transform.position, entranceDirection);
        if (savedHealth > 0f) boss.Health.RestoreHealth(savedHealth);
        boss.Health.Died += OnBossDied;
    }

    private void OnBossDied()
    {
        if (boss != null) boss.Health.Died -= OnBossDied;
        bossAlive = false;
        savedHealth = 0f;
        defeatedCount++;
        BossDefeated?.Invoke(this);
    }

    // 비어 있는 둥지에 보스를 다시 부름 (제단)
    public bool Summon()
    {
        if (bossAlive) return false;

        foreach (ChargeBlocker pillar in GetComponentsInChildren<ChargeBlocker>(true)) pillar.Restore();
        BossRock.ClearAll();

        bossAlive = true;
        savedHealth = 0f;
        SpawnBoss();
        return true;
    }

    #region 저장

    public string SaveKey => "nest";

    public JToken Save()
    {
        var state = new JObject { ["alive"] = bossAlive };
        if (bossAlive && boss != null && !boss.Health.IsDead) state["hp"] = boss.Health.CurrentHealth;
        if (defeatedCount > 0) state["defeated"] = defeatedCount;
        return state;
    }

    public void Load(JToken data)
    {
        bossAlive = data.Value<bool?>("alive") ?? true;
        savedHealth = data.Value<float?>("hp") ?? 0f;
        defeatedCount = data.Value<int?>("defeated") ?? 0;
    }

    #endregion
}
