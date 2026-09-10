using UnityEngine;

public enum PlayerActionState { Normal, Dodge, Attack }
public class PlayerState : MonoBehaviour
{
    public Vector2 LastDir { get; private set; }
    public PlayerActionState CurrentState { get; private set; }

    // 새로운 액션(공격 또는 회피)을 시작해도 되는지 여부
    public bool CanAct => CurrentState == PlayerActionState.Normal;

    public void SetLastDir(Vector2 lastDir)
    {
        LastDir = lastDir;
    }
    public void SetState(PlayerActionState newState)
    {
        CurrentState = newState;
    }
}
