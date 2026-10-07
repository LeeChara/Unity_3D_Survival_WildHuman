using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 타이틀 화면 버튼 처리와 진입·퇴장 시 화면 페이드
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private ScreenFader fader;
    [Tooltip("시작 시 선택해 둘 버튼 (키보드 조작용, 선택된 버튼이 강조 표시됨)")]
    [SerializeField] private GameObject firstSelected;
    [SerializeField] private SettingsUI settingsUI;
    [Tooltip("설정 창을 닫으면 다시 선택할 버튼")]
    [SerializeField] private GameObject settingsButton;

    private bool isLeaving;
    // 설정 창의 Esc를 받기 위한 입력 (일시정지 메뉴와 같은 System 맵)
    private PlayerAction actions;

    private void Awake()
    {
        actions = new PlayerAction();
        InputBindings.Register(actions.asset);
    }

    private void OnEnable()
    {
        actions.System.Pause.performed += OnBackPerformed;
        actions.System.Enable();
        settingsUI.Closed += OnSettingsClosed;
    }

    private void OnDisable()
    {
        actions.System.Pause.performed -= OnBackPerformed;
        actions.System.Disable();
        settingsUI.Closed -= OnSettingsClosed;
    }

    private void OnDestroy()
    {
        InputBindings.Unregister(actions.asset);
        actions.Dispose();
    }

    private void Start()
    {
        fader.FadeIn();
        if (firstSelected != null) EventSystem.current.SetSelectedGameObject(firstSelected);
    }

    // 버튼 OnClick에 연결
    public void StartGame()
    {
        Leave(SceneFlow.LoadWorldSelect);
    }

    public void QuitGame()
    {
        Leave(SceneFlow.Quit);
    }

    public void OpenSettings()
    {
        if (!isLeaving) settingsUI.Open();
    }

    private void OnBackPerformed(InputAction.CallbackContext ctx)
    {
        if (settingsUI.IsOpen) settingsUI.Back();
    }

    private void OnSettingsClosed()
    {
        if (settingsButton != null) EventSystem.current.SetSelectedGameObject(settingsButton);
    }

    private void Leave(Action onFaded)
    {
        if (isLeaving) return;
        isLeaving = true;
        fader.FadeOutThen(onFaded);
    }
}
