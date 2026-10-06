using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Esc로 여는 일시정지 메뉴. 열려 있는 동안 게임 시간과 플레이어 조작을 멈춤
public class PauseMenuUI : MonoBehaviour
{
    // 이 스크립트는 항상 활성 상태인 부모에 두고, 패널만 켜고 끔
    [SerializeField] private GameObject panel;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("Esc를 눌렀을 때 열려 있으면 일시정지 대신 이것부터 닫음")]
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private SettingsUI settingsUI;
    [Tooltip("일시정지 메뉴를 열거나 설정 창을 닫으면 선택할 버튼")]
    [SerializeField] private GameObject resumeButton;
    [SerializeField] private GameObject settingsButton;

    public bool IsPaused { get; private set; }
    public event Action<bool> PausedChanged;

    // 사망 중에는 PlayerInput 전체가 꺼지므로, Esc는 별도 인스턴스의 System 맵으로 받음
    private PlayerAction actions;

    private void Awake()
    {
        actions = new PlayerAction();
        InputBindings.Register(actions.asset);

        // 시작 시 닫힌 상태로 통일
        panel.SetActive(false);
    }

    private void OnEnable()
    {
        actions.System.Pause.performed += OnPausePerformed;
        actions.System.Enable();
        settingsUI.Closed += OnSettingsClosed;
    }

    private void OnDisable()
    {
        // 재활성화 시 중복 등록되지 않도록 OnEnable과 짝을 맞춰 해제
        actions.System.Pause.performed -= OnPausePerformed;
        actions.System.Disable();
        settingsUI.Closed -= OnSettingsClosed;
    }

    private void OnDestroy()
    {
        InputBindings.Unregister(actions.asset);
        actions.Dispose();

        // 씬 전환 외의 경로(Play 종료 등)로 파괴될 때도 시간 배율이 멈춘 채 남지 않도록 복구
        if (IsPaused) Time.timeScale = 1f;
    }

    private void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        // 설정 창이 열려 있으면 Esc는 설정 창의 뒤로 가기
        if (settingsUI.IsOpen)
        {
            settingsUI.Back();
            return;
        }

        if (!IsPaused && inventoryUI != null && inventoryUI.IsOpen)
        {
            inventoryUI.SetOpen(false);
            return;
        }

        SetPaused(!IsPaused);
    }

    public void SetPaused(bool paused)
    {
        if (IsPaused == paused) return;

        IsPaused = paused;
        if (!paused) settingsUI.Close();
        panel.SetActive(paused);
        if (paused) Select(resumeButton);
        Time.timeScale = paused ? 0f : 1f;

        // 시간이 멈춰도 입력 콜백은 계속 오므로 조작을 직접 차단
        // 끌 때 이동·달리기 입력이 취소되어 재개 후 값이 남지 않음
        if (paused)
        {
            playerInput.DeactivateInput();
        }
        else if (!playerHealth.IsDead)
        {
            // 사망 중이면 부활할 때 PlayerHealth가 다시 켬
            playerInput.ActivateInput();
        }

        PausedChanged?.Invoke(paused);
    }

    // 버튼 OnClick에 연결
    public void Resume()
    {
        SetPaused(false);
    }

    public void OpenSettings()
    {
        settingsUI.Open();
    }

    private void OnSettingsClosed()
    {
        Select(settingsButton);
    }

    private static void Select(GameObject target)
    {
        if (EventSystem.current != null && target != null) EventSystem.current.SetSelectedGameObject(target);
    }

    public void GoToTitle()
    {
        SceneFlow.LoadTitle();
    }

    public void QuitGame()
    {
        SceneFlow.Quit();
    }
}
