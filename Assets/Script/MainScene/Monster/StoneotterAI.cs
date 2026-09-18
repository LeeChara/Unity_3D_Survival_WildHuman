using UnityEngine;

public class StoneotterAI : MonsterAI
{
    [SerializeField] private Transform throwPoint;

    protected StoneotterData otterData;
    private int stonesThrown;

    protected override void Awake()
    {
        base.Awake();
        otterData = (StoneotterData)data;
    }

    protected override void Update()
    {
        base.Update();

        if (target != null && (state == State.Windup || state == State.Attack) && rb.linearVelocity.sqrMagnitude <= 0.01f)
        {
            monsterFacing.UpdateFacing(targetDirection, cameraRight, cameraForwardFlat);
        }
    }
    protected override void Chase()
    {
        if (target == null)
        {
            state = State.Idle;

            animator.SetTrigger("Reset");
            return;
        }

        if (targetDistance > data.windupRange)
        {
            rb.linearVelocity = targetDirection * data.chaseSpeed;
            transform.rotation = Quaternion.LookRotation(targetDirection);
        }
        else if (targetDistance < otterData.windupMinRange) {
            rb.linearVelocity = -targetDirection * data.chaseSpeed;
            transform.rotation = Quaternion.LookRotation(-targetDirection);
        }
        else
        {
            state = State.Windup;

            stateTime = 0f;

            animator.SetTrigger("Windup");
        }
    }

    protected override void Windup()
    {
        if (target == null)
        {
            state = State.Idle;

            animator.SetTrigger("Reset");
            return;
        }

        WindupBehavior();

        if (stateTime > data.windupDuration)
        {
            state = State.Attack;
            stateTime = 0f;

            animator.SetTrigger("Attack");

            stonesThrown = 0;
        }
    }

    protected override void Attack()
    {
        if (target == null)
        {
            state = State.Recover;
            stateTime = 0f;

            animator.SetTrigger("Recover");
            return;
        }

        AttackBehavior();

        if (stonesThrown < otterData.throwCount && stateTime >= stonesThrown * otterData.throwInterval)
        {
            ThrowStone();
            stonesThrown++;
        }

        if (stateTime > data.attackDuration)
        {
            state = State.Recover;
            stateTime = 0f;

            animator.SetTrigger("Recover");
        }
    }

    protected override void AttackBehavior()
    {
        rb.linearVelocity = Vector3.zero;
        transform.rotation = Quaternion.LookRotation(targetDirection);
    }

    private void ThrowStone()
    {
        Vector3 spawnPosition = throwPoint != null ? throwPoint.position : transform.position;
        Projectile projectile = Instantiate(otterData.stonePrefab, spawnPosition, Quaternion.identity);
        projectile.Init(targetDirection, otterData.stoneSpeed, (int)data.attackDamage, playerLayer);
    }
}
