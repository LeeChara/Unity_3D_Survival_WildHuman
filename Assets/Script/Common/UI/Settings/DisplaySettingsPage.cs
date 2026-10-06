using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 화면 탭: 해상도·화면 모드는 고른 뒤 "적용"을 눌러야 바뀌고, 일정 시간 안에 유지를 누르지 않으면 되돌림
// 수직 동기화·프레임 제한은 바로 적용
public class DisplaySettingsPage : SettingsPage
{
    private static readonly FullScreenMode[] Modes =
    {
        FullScreenMode.ExclusiveFullScreen,
        FullScreenMode.FullScreenWindow,
        FullScreenMode.Windowed,
    };
    private static readonly string[] ModeNames = { "전체 화면", "테두리 없는 창", "창 모드" };
    private static readonly string[] OnOff = { "끄기", "켜기" };
    // 0은 제한 없음
    private static readonly int[] FrameLimits = { 30, 60, 120, 144, 240, 0 };

    [SerializeField] private OptionSelector resolutionSelector;
    [SerializeField] private OptionSelector screenModeSelector;
    [SerializeField] private OptionSelector vSyncSelector;
    [SerializeField] private OptionSelector frameLimitSelector;
    [Tooltip("아래 버튼 줄의 적용 버튼 (모든 탭에서 보임)")]
    [SerializeField] private Button applyButton;

    [Header("확인 창")]
    [SerializeField] private GameObject confirmDialog;
    [Tooltip("{0}에 남은 초가 들어감")]
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private string confirmFormat = "이 화면 설정을 유지할까요?\n{0}초 후 되돌립니다";
    [SerializeField] private Button keepButton;
    [SerializeField] private Button revertButton;
    [SerializeField] private float confirmSeconds = 10f;

    private readonly List<Vector2Int> resolutions = new();

    // 확인 창이 떠 있는 동안 되돌아갈 값
    private Vector2Int previousResolution;
    private FullScreenMode previousMode;
    private float confirmRemaining;
    private bool IsConfirming => confirmDialog.activeSelf;

    private void Awake()
    {
        vSyncSelector.ValueChanged += index => GameOptions.SetVSync(index == 1);
        frameLimitSelector.ValueChanged += index => GameOptions.SetFrameLimit(FrameLimits[index]);
        applyButton.onClick.AddListener(ApplyDisplay);
        keepButton.onClick.AddListener(Keep);
        revertButton.onClick.AddListener(Revert);

        confirmDialog.SetActive(false);
    }

    public override void Refresh()
    {
        Vector2Int current = new(Screen.width, Screen.height);

        // 주사율만 다른 해상도는 하나로 합치고, 현재 해상도가 목록에 없으면(창 크기를 직접 바꾼 경우 등) 추가
        resolutions.Clear();
        resolutions.AddRange(Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct());
        if (!resolutions.Contains(current)) resolutions.Add(current);
        resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        resolutionSelector.SetOptions(resolutions.Select(r => $"{r.x} x {r.y}"), resolutions.IndexOf(current));
        screenModeSelector.SetOptions(ModeNames, ModeIndex(Screen.fullScreenMode));
        vSyncSelector.SetOptions(OnOff, GameOptions.VSync ? 1 : 0);

        int limitIndex = System.Array.IndexOf(FrameLimits, GameOptions.FrameLimit);
        frameLimitSelector.SetOptions(FrameLimits.Select(f => f > 0 ? $"{f}" : "제한 없음"), limitIndex >= 0 ? limitIndex : FrameLimits.Length - 1);
    }

    public override void ResetToDefault()
    {
        // 해상도·화면 모드는 모니터마다 다르므로 그대로 두고 바로 적용되는 항목만 초기화
        GameOptions.SetVSync(true);
        GameOptions.SetFrameLimit(0);
        Refresh();
    }

    public override bool HandleBack()
    {
        if (!IsConfirming) return false;

        Revert();
        return true;
    }

    public override void OnHide()
    {
        // 확인하지 않은 채 닫으면 되돌림 (실수로 볼 수 없는 화면이 된 경우 대비)
        if (IsConfirming)
        {
            Revert();
            return;
        }

        // 적용하지 않은 선택은 버림. 적용 버튼은 모든 탭에서 보이므로, 다른 탭에서 눌렀을 때 보이지 않는 값이 적용되지 않게 함
        int index = resolutions.IndexOf(new Vector2Int(Screen.width, Screen.height));
        if (index >= 0) resolutionSelector.SetIndexWithoutNotify(index);
        screenModeSelector.SetIndexWithoutNotify(ModeIndex(Screen.fullScreenMode));
    }

    private void Update()
    {
        if (!IsConfirming) return;

        // 일시정지 중에도 줄어들도록 실제 시간 기준
        confirmRemaining -= Time.unscaledDeltaTime;
        if (confirmRemaining <= 0f)
        {
            Revert();
            return;
        }
        confirmText.text = string.Format(confirmFormat, Mathf.CeilToInt(confirmRemaining));
    }

    private void ApplyDisplay()
    {
        if (resolutions.Count == 0) return;

        Vector2Int resolution = resolutions[resolutionSelector.Index];
        FullScreenMode mode = Modes[screenModeSelector.Index];
        if (resolution == new Vector2Int(Screen.width, Screen.height) && mode == Screen.fullScreenMode) return;

        previousResolution = new Vector2Int(Screen.width, Screen.height);
        previousMode = Screen.fullScreenMode;
        GameOptions.SetDisplay(resolution, mode);

        confirmRemaining = confirmSeconds;
        confirmText.text = string.Format(confirmFormat, Mathf.CeilToInt(confirmRemaining));
        confirmDialog.SetActive(true);
        Select(keepButton);
    }

    private void Keep()
    {
        confirmDialog.SetActive(false);
        Select(applyButton);
    }

    private void Revert()
    {
        confirmDialog.SetActive(false);
        GameOptions.SetDisplay(previousResolution, previousMode);

        // Screen.width 등은 다음 프레임에 바뀌므로 선택지는 되돌린 값으로 직접 맞춤
        int index = resolutions.IndexOf(previousResolution);
        if (index >= 0) resolutionSelector.SetIndexWithoutNotify(index);
        screenModeSelector.SetIndexWithoutNotify(ModeIndex(previousMode));
        Select(applyButton);
    }

    private static int ModeIndex(FullScreenMode mode)
    {
        // MaximizedWindow는 macOS 전용이라 창 모드로 표시
        int index = System.Array.IndexOf(Modes, mode);
        return index >= 0 ? index : Modes.Length - 1;
    }

    private static void Select(Selectable target)
    {
        if (EventSystem.current != null && target != null && target.gameObject.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
    }
}
