using UnityEngine;

// 음파에 반응하는 생물 (플레이어, 몬스터)
// 땅속에 숨어 있으면 즉시 길게 기절, 아니면 음파 바깥쪽으로 밀려나며 짧게 기절 (피해 없음)
[RequireComponent(typeof(Health))]
public class SoundReceiver : MonoBehaviour, ISoundReactive
{
    private Health health;
    private Knockback knockback;
    private IStunnable stunnable;
    private IBurrowable burrowable;

    private void Awake()
    {
        health = GetComponent<Health>();
        knockback = GetComponent<Knockback>();
        stunnable = GetComponent<IStunnable>();
        burrowable = GetComponent<IBurrowable>();
    }

    public void OnSoundWave(SoundWave wave)
    {
        if (health.IsDead) return;

        SoundWaveSetting setting = wave.Setting;
        if (burrowable != null && burrowable.IsBurrowed)
        {
            stunnable?.Stun(setting.burrowedStunDuration);
            return;
        }

        // 기절이 먼저 걸려야 공격 중 슈퍼아머가 풀려서 넉백도 들어감
        stunnable?.Stun(setting.stunDuration);

        if (knockback != null)
        {
            Vector3 direction = transform.position - wave.Origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = wave.Direction;
            knockback.Apply(direction, setting.knockbackDistance * wave.Power);
        }
    }
}
