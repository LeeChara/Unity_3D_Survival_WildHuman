using System.Collections.Generic;
using UnityEngine;

public class WeaponHitboxController : MonoBehaviour
{
    [SerializeField] private LayerMask enemyLayer;

    // 한 번의 공격동안 같은 적을 중복으로 공격하지 않도록 추적
    private HashSet<Collider> hitEnemies = new HashSet<Collider>();

    private void OnTriggerEnter(Collider other)
    {
        // enemyLayer에 해당하는 레이어가 아닌 경우 리턴
        if ((enemyLayer.value & (1 << other.gameObject.layer)) == 0) return;
        if (hitEnemies.Contains(other)) return;

        hitEnemies.Add(other);
        Debug.Log($"Hit {other.gameObject.name}");
    }

    // 새로운 공격을 시작할 때 초기화
    public void ResetHitEnemies()
    {
        hitEnemies.Clear();
    }
}
