using UnityEngine;

public class EnemyHealth : Health
{
    protected override void Die()
    {
        base.Die();
        Destroy(gameObject);
    }
}
