using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    private enum State
    {
        Idle,
        Chase,
        WindUp,
        Charge
    }
    private Rigidbody rb;
    public Transform player;

    [SerializeField] private GameObject hitbox;
    [SerializeField] private HitboxController hitboxController;

    public float detectionRange = 10f;
    public float chargeRange = 2f;

    public float moveSpeed = 3f;
    public float chargeSpeed = 6f;

    public float windUpDuration = 3f;
    public float chargeDuration = 1.5f;

    private State state = State.Idle;
    private float stateTime;
    private Vector3 chargeDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        hitbox.SetActive(false);
    }

    private void Update()
    {
        float playerDistance = Vector3.Distance(transform.position, player.position);
        Vector3 playerDir = player.position - transform.position;
        playerDir.y = 0;
        playerDir.Normalize();

        // 상태는 오로지 하나만 가지고 있어야 함
        switch (state)
        {
            case State.Idle:
                if (playerDistance <= detectionRange)
                {
                    state = State.Chase;
                }
                break;

            case State.Chase:
                rb.linearVelocity = playerDir * moveSpeed;
                transform.rotation = Quaternion.LookRotation(playerDir);
                if (playerDistance <= chargeRange)
                {
                    state = State.WindUp;
                    stateTime = 0f;
                }
                else if (playerDistance > detectionRange)
                {
                    state = State.Idle;
                    rb.linearVelocity = Vector3.zero;
                }
                break;

            case State.WindUp:
                rb.linearVelocity = Vector3.zero;
                stateTime += Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(playerDir);
                if (stateTime >= windUpDuration)
                {
                    chargeDirection = playerDir;

                    state = State.Charge;
                    stateTime = 0f;

                    hitboxController.ResetHitTargets();
                    hitbox.SetActive(true);
                }
                break;

            case State.Charge:
                rb.linearVelocity = chargeDirection * chargeSpeed;
                stateTime += Time.deltaTime;
                if (stateTime >= chargeDuration)
                {
                    state = State.Idle;
                    rb.linearVelocity = Vector3.zero;

                    hitbox.SetActive(false);
                }
                break;
        }
    }
}
