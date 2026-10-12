using System;
using UnityEngine;

// Stunned: 음파 등으로 기절 (이동·공격·회피·아이템 사용 불가)
public enum PlayerActionState { Normal, Dodge, Attack, Knockback, Dead, Stunned }
public class PlayerState : MonoBehaviour, IStunnable
{
    public Vector2 LastDir { get; private set; }
    public PlayerActionState CurrentState { get; private set; }

    // 새로운 액션(공격 또는 회피)을 시작해도 되는지 여부
    public bool CanAct => CurrentState == PlayerActionState.Normal;

    private Knockback knockback;
    // 기절 남은 시간 (넉백 중에는 흐르지 않고, 넉백이 끝나면 이어서 기절)
    private float stunRemaining;

    // 기절이 걸린 시점 (진행 중인 공격 취소 등)
    public event Action StunStarted;

    // 넉백으로 밀려나는 중에도 기절이 걸려 있으면 true
    public bool IsStunned => stunRemaining > 0f && CurrentState != PlayerActionState.Dead;

    private void Awake()
    {
        knockback = GetComponent<Knockback>();
        if (knockback == null) return;

        // 회피 중에는 넉백 무시
        knockback.IsImmune = () => CurrentState == PlayerActionState.Dodge;
        knockback.Started += () => SetState(PlayerActionState.Knockback);
        knockback.Ended += OnKnockbackEnded;
    }

    private void Update()
    {
        if (CurrentState != PlayerActionState.Stunned) return;

        stunRemaining -= Time.deltaTime;
        if (stunRemaining > 0f) return;

        stunRemaining = 0f;
        SetState(PlayerActionState.Normal);
    }

    private void OnKnockbackEnded()
    {
        SetState(stunRemaining > 0f ? PlayerActionState.Stunned : PlayerActionState.Normal);
    }

    // 회피 중(무적)이거나 사망 중이면 걸리지 않음. 이미 기절 중이면 남은 시간과 새 시간 중 긴 쪽
    public void Stun(float duration)
    {
        if (duration <= 0f) return;
        if (CurrentState == PlayerActionState.Dead || CurrentState == PlayerActionState.Dodge) return;

        stunRemaining = Mathf.Max(stunRemaining, duration);
        if (CurrentState != PlayerActionState.Knockback) SetState(PlayerActionState.Stunned);
        StunStarted?.Invoke();
    }

    public void SetLastDir(Vector2 lastDir)
    {
        LastDir = lastDir;
    }
    // 사망 중에는 넉백 종료 등으로 다른 상태로 바뀌지 않음 (Revive로만 해제)
    public void SetState(PlayerActionState newState)
    {
        if (CurrentState == PlayerActionState.Dead) return;
        CurrentState = newState;
    }

    public void Revive()
    {
        stunRemaining = 0f;
        CurrentState = PlayerActionState.Normal;
    }
}
