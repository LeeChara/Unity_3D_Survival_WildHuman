using UnityEngine;

public class StoneotterAI : MonsterAI
{
    [SerializeField] private Transform throwPoint;
    [SerializeField] private LayerMask stoneTargetLayer; // 돌멩이가 맞힐 대상 (인식용 playerLayer와 별개)

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

            PlayAnimation(State.Idle);
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

            PlayAnimation(State.Windup);
        }
    }

    protected override void Windup()
    {
        if (target == null)
        {
            state = State.Idle;

            PlayAnimation(State.Idle);
            return;
        }

        WindupBehavior();

        if (stateTime > data.windupDuration)
        {
            state = State.Attack;
            stateTime = 0f;

            PlayAnimation(State.Attack);

            stonesThrown = 0;
        }
    }

    protected override void Attack()
    {
        if (target == null)
        {
            state = State.Recover;
            stateTime = 0f;

            PlayAnimation(State.Recover);
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

            PlayAnimation(State.Recover);
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

        // 생성 위치 기준으로 조준해야 ThrowPoint 오프셋만큼 빗나가지 않음
        Vector3 throwDirection = target.position - spawnPosition;
        throwDirection.y = 0f;

        Projectile projectile = Instantiate(otterData.stonePrefab, spawnPosition, Quaternion.identity);
        projectile.Init(throwDirection, otterData.stoneSpeed, otterData.projectile, stoneTargetLayer);
    }
}
