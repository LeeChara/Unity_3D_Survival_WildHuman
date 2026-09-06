using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private GameObject weapon;
    [SerializeField] private GameObject weaponHitbox;
    [SerializeField] private HitboxController weaponHitboxController; // WeaponHitbox와 같은 오브젝트

    public float attackDuration = 0.3f;
    public float recoverDuration = 0.5f;

    private enum AttackPhase
    {
        None,
        Attack,
        Recover
    }
    private AttackPhase attackPhase = AttackPhase.None;
    private float phaseTime;
    private void Awake()
    {
        weaponHitbox.SetActive(false);
    }

    void OnAttack(InputValue value)
    {
        if (!value.isPressed) return;
        if (attackPhase != AttackPhase.None) return;

        attackPhase = AttackPhase.Attack;
        phaseTime = 0f;

        weaponHitboxController.ResetHitEnemies(); 
        weaponHitbox.SetActive(true);

        weapon.transform.localPosition += new Vector3(0, 0, 0.4f);
    }

    private void Update()
    {
        if (attackPhase == AttackPhase.None) return;

        phaseTime += Time.deltaTime;

        switch (attackPhase)
        {
            case AttackPhase.Attack:
                if (phaseTime >= attackDuration)
                {
                    attackPhase = AttackPhase.Recover;
                    phaseTime = 0f;

                    weapon.transform.localPosition -= new Vector3(0, 0, 0.4f);
                    weaponHitbox.SetActive(false);
                }
                break;

            case AttackPhase.Recover:
                if (phaseTime >= recoverDuration)
                {
                    attackPhase = AttackPhase.None;
                }
                break;
        }
    }
}
