using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 타이틀·일시정지 메뉴에서 같이 쓰는 설정 창. 탭 전환, 키보드 내비게이션, 기본값·닫기를 담당
// Esc 입력은 여는 쪽(MainMenuUI, PauseMenuUI)이 받아서 Back()으로 넘김
public class SettingsUI : MonoBehaviour
{
    [Serializable]
    private class Tab
    {
        public Button button;
        [Tooltip("선택되지 않은 탭을 흐리게 표시")]
        public CanvasGroup group;
        public SettingsPage page;
    }

    // 이 스크립트는 항상 활성 상태인 부모에 두고, 패널만 켜고 끔
    [SerializeField] private GameObject panel;
    [SerializeField] private Tab[] tabs;
    [SerializeField] private Button defaultButton;
    [Tooltip("화면 탭의 해상도·화면 모드 적용 (클릭 처리는 DisplaySettingsPage가 담당, 여기서는 내비게이션만)")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private float inactiveTabAlpha = 0.55f;

    public bool IsOpen => panel.activeSelf;
    public event Action Closed;

    private int current;

    private void Awake()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].button.onClick.AddListener(() => ShowTab(index));
        }
        defaultButton.onClick.AddListener(ResetCurrentTab);
        closeButton.onClick.AddListener(Close);

        panel.SetActive(false);
    }

    public void Open()
    {
        if (IsOpen) return;

        panel.SetActive(true);
        ShowTab(current);
        Select(tabs[current].button);
    }

    public void Close()
    {
        if (!IsOpen) return;

        tabs[current].page.OnHide();
        panel.SetActive(false);
        Closed?.Invoke();
    }

    // Esc: 페이지가 처리할 일(키 입력 대기, 확인 창)이 있으면 그것부터, 없으면 닫기
    public void Back()
    {
        if (!IsOpen) return;
        if (tabs[current].page.HandleBack()) return;

        Close();
    }

    private void ShowTab(int index)
    {
        if (index != current) tabs[current].page.OnHide();
        current = index;

        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = i == current;
            tabs[i].page.gameObject.SetActive(active);
            tabs[i].group.alpha = active ? 1f : inactiveTabAlpha;
        }

        // 페이지를 켠 뒤(항목들의 Awake 이후) 값을 채움
        tabs[current].page.Refresh();
        BuildNavigation();
    }

    private void ResetCurrentTab()
    {
        tabs[current].page.ResetToDefault();
    }

    // 탭 ↔ 현재 페이지 항목 ↔ 아래 버튼(기본값·적용·닫기)을 위아래로 잇고, 탭과 아래 버튼은 좌우로 이음
    // 항목의 좌우는 비워 두어 슬라이더·선택지가 값 변경에 씀
    private void BuildNavigation()
    {
        Selectable[] rows = tabs[current].page.Rows;
        Selectable firstRow = rows.Length > 0 ? rows[0] : defaultButton;
        Selectable lastRow = rows.Length > 0 ? rows[^1] : tabs[current].button;

        for (int i = 0; i < tabs.Length; i++)
        {
            SetNavigation(tabs[i].button,
                up: null,
                down: firstRow,
                left: tabs[(i - 1 + tabs.Length) % tabs.Length].button,
                right: tabs[(i + 1) % tabs.Length].button);
        }

        for (int i = 0; i < rows.Length; i++)
        {
            SetNavigation(rows[i],
                up: i > 0 ? rows[i - 1] : tabs[current].button,
                down: i < rows.Length - 1 ? rows[i + 1] : defaultButton,
                left: null,
                right: null);
        }

        SetNavigation(defaultButton, up: lastRow, down: null, left: closeButton, right: applyButton);
        SetNavigation(applyButton, up: lastRow, down: null, left: defaultButton, right: closeButton);
        SetNavigation(closeButton, up: lastRow, down: null, left: applyButton, right: defaultButton);
    }

    private static void SetNavigation(Selectable target, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        target.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = left,
            selectOnRight = right,
        };
    }

    private static void Select(Selectable target)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target.gameObject);
    }
}
