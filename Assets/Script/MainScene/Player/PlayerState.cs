using UnityEngine;

public enum PlayerActionState { Normal, Dodge, Attack, Knockback, Dead }
public class PlayerState : MonoBehaviour
{
    public Vector2 LastDir { get; private set; }
    public PlayerActionState CurrentState { get; private set; }

    // 새로운 액션(공격 또는 회피)을 시작해도 되는지 여부
    public bool CanAct => CurrentState == PlayerActionState.Normal;

    private Knockback knockback;

    private void Awake()
    {
        knockback = GetComponent<Knockback>();
        if (knockback == null) return;

        // 회피 중에는 넉백 무시
        knockback.IsImmune = () => CurrentState == PlayerActionState.Dodge;
        knockback.Started += () => SetState(PlayerActionState.Knockback);
        knockback.Ended += () => SetState(PlayerActionState.Normal);
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
        CurrentState = PlayerActionState.Normal;
    }
}
