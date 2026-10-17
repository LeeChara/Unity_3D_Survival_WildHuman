using UnityEditor;
using UnityEngine;

// 보스 시험용 메뉴 (플레이 모드, 개발용)
// 둥지 구조물이 생기기 전까지 플레이어 근처에 보스를 직접 소환하고, 페이즈 전환을 바로 시험할 수 있게 함
public static class BossTestMenu
{
    private const string SunBearPath = "Assets/Prefabs/Boss/SunBear.prefab";
    // 플레이어에게서 떨어진 소환 거리 (인식 범위 밖에서 다가오도록)
    private const float SpawnDistance = 16f;

    [MenuItem("Tools/보스/태양곰 소환")]
    private static void SpawnSunBear()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SunBearPath);
        PlayerHealth player = Object.FindFirstObjectByType<PlayerHealth>();
        if (prefab == null || player == null)
        {
            Debug.LogWarning("[보스 시험] 태양곰 프리팹이나 플레이어를 찾지 못함");
            return;
        }

        Vector3 position = player.transform.position + Quaternion.Euler(0f, 45f, 0f) * Vector3.right * SpawnDistance;
        GameObject boss = Object.Instantiate(prefab, position, Quaternion.identity);
        Selection.activeGameObject = boss;
    }

    [MenuItem("Tools/보스/선택한 보스 체력 30%로")]
    private static void DropToPhase2() => SetHealthRatio(0.29f);

    [MenuItem("Tools/보스/선택한 보스 체력 5%로")]
    private static void DropToNearDeath() => SetHealthRatio(0.05f);

    [MenuItem("Tools/보스/태양곰 소환", true)]
    private static bool CanSpawn() => Application.isPlaying;

    [MenuItem("Tools/보스/선택한 보스 체력 30%로", true)]
    [MenuItem("Tools/보스/선택한 보스 체력 5%로", true)]
    private static bool HasSelectedBoss() => Application.isPlaying && SelectedBoss() != null;

    private static BossAI SelectedBoss() =>
        Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<BossAI>() : null;

    private static void SetHealthRatio(float ratio)
    {
        BossAI boss = SelectedBoss();
        Health health = boss.Health;
        int damage = Mathf.FloorToInt(health.CurrentHealth - health.MaxHealth * ratio);
        if (damage > 0) health.TakeDamage(damage);
    }
}
