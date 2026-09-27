using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 3f;

    private Vector3 direction;
    private float speed;
    private AttackData attack;
    private LayerMask targetLayer;

    public void Init(Vector3 direction, float speed, AttackData attack, LayerMask targetLayer)
    {
        this.direction = direction.normalized;
        this.direction.y = 0f;
        this.speed = speed;
        this.attack = attack;
        this.targetLayer = targetLayer;

        if (this.direction.sqrMagnitude > 0f)
        {
            transform.rotation = Quaternion.LookRotation(this.direction);
        }
    }

    private void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((targetLayer.value & (1 << other.gameObject.layer)) == 0) return;

        if (other.TryGetComponent<Health>(out Health health))
        {
            health.TakeDamage(attack.damage);
            Debug.Log($"{other.name} 피격(돌멩이) - 현재 체력: {health.CurrentHealth} / {health.MaxHealth}");
        }

        // 투사체는 날아가던 방향으로 밀어냄
        if (other.TryGetComponent<Knockback>(out Knockback knockback))
        {
            knockback.Apply(direction, attack.knockbackDistance);
        }

        Destroy(gameObject);
    }
}
