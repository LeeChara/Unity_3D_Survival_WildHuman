using System.Collections.Generic;
using UnityEngine;

// 보스가 던져서 바닥에 남은 바위 (자원처럼 캘 수 있고, 보스가 돌진으로 부딪히면 보스는 기절하고 바위는 부서짐)
// 청크에 속한 자원이 아니라 저장하지 않고, 쓰러지면 풀로 돌아가는 대신 스스로 제거됨
// 너무 쌓이지 않도록 동시에 남는 개수를 제한 (넘으면 가장 오래된 것부터 부서짐)
[RequireComponent(typeof(PropHealth))]
public class BossRock : MonoBehaviour
{
    private static readonly List<BossRock> alive = new();

    private PropHealth health;

    public static BossRock Spawn(BossRock prefab, Vector3 position, int maxCount)
    {
        while (maxCount > 0 && alive.Count >= maxCount)
        {
            BossRock oldest = alive[0];
            alive.RemoveAt(0);
            if (oldest != null) oldest.health.Kill();
        }

        return Instantiate(prefab, new Vector3(position.x, prefab.transform.position.y, position.z), Quaternion.identity);
    }

    // 남아 있는 바위를 모두 없앰 (보스를 다시 부를 때 둥지 정리, 드랍 없음)
    public static void ClearAll()
    {
        foreach (BossRock rock in alive.ToArray())
        {
            if (rock != null) Destroy(rock.gameObject);
        }
        alive.Clear();
    }

    private void Awake()
    {
        health = GetComponent<PropHealth>();
        health.Depleted += OnDepleted;
        alive.Add(this);
    }

    private void OnDestroy()
    {
        alive.Remove(this);
        if (health != null) health.Depleted -= OnDepleted;
    }

    private void OnDepleted(PropHealth _)
    {
        Destroy(gameObject);
    }
}
