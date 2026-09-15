using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    private PlayerAction actions;

    public Vector2 MoveInput { get; private set; }
    public bool IsSprintPressed { get; private set; }
    public bool IsDodgePressed { get; private set; }

    private void Awake()
    {
        actions = new PlayerAction();
    }

    private void OnEnable()
    {
        // Move 액션
        actions.Player.Move.performed += OnMovePerformed;
        actions.Player.Move.canceled += OnMoveCanlceled;

        // Sprint 액션
        actions.Player.Sprint.performed += OnSprintPerformed;
        actions.Player.Sprint.canceled += OnSprintCanceled;

        // Dodge 액션
        actions.Player.Dodge.performed += OnDodgePerformed;

        actions.Player.Enable();
    }

    private void OnDisable()
    {
        // 재활성화 시 중복 등록되지 않도록 이벤트 구독 해제
        // OnEnable에서 구독한 것과 짝을 맞춰 해제

        // Move 액션
        actions.Player.Move.performed -= OnMovePerformed;
        actions.Player.Move.canceled -= OnMoveCanlceled;

        // Sprint 액션
        actions.Player.Sprint.performed -= OnSprintPerformed;
        actions.Player.Sprint.canceled -= OnSprintCanceled;

        // Dodge 액션
        actions.Player.Dodge.performed -= OnDodgePerformed;

        actions.Player.Disable();

        // 이전의 상태가 남지 않도록 초기화
        MoveInput = Vector2.zero;
        IsSprintPressed = false;
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        MoveInput = ctx.ReadValue<Vector2>();
    }
    private void OnMoveCanlceled(InputAction.CallbackContext ctx)
    {
        // 이동하지 않을 때, 방향값이 남지 않도록 초기화
        MoveInput = Vector2.zero;
    }
    private void OnSprintPerformed(InputAction.CallbackContext ctx)
    {
        IsSprintPressed = true;
    }

    private void OnSprintCanceled(InputAction.CallbackContext ctx)
    {
        IsSprintPressed = false;
    }
    private void OnDodgePerformed(InputAction.CallbackContext ctx)
    {
        // TODO: 구르기 로직 작성
    }
}
