using UnityEngine;

public class MonsterHealth : Health
{
    protected override void Die()
    {
        base.Die();
        Destroy(gameObject);
    }
}
