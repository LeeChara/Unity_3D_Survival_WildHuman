using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 조작 탭: 항목을 누르면 다음에 누른 키(또는 마우스 버튼)로 바꿈. Esc는 취소
// 다른 항목이 이미 쓰는 키면 서로 맞바꾸고, 핫바 숫자키처럼 이 화면에 없는 키와 겹치면 바꾸지 않음
// 설정 화면 전용 입력 에셋에서 바꾼 뒤 InputBindings로 저장해 게임에서 쓰는 에셋들에 반영
public class ControlsSettingsPage : SettingsPage
{
    [SerializeField] private RebindRow[] rebindRows;
    [SerializeField] private string conflictText = "다른 조작에 쓰는 키";
    [SerializeField] private float conflictShowSeconds = 1.2f;

    private PlayerAction actions;
    private InputActionRebindingExtensions.RebindingOperation operation;
    private RebindRow rebindingRow;
    // 키 입력 대기를 끝낸 프레임 (그 입력이 버튼 클릭으로도 처리되어 다시 대기에 들어가는 것을 막음)
    private int lastRebindFrame = -1;
    private RebindRow conflictRow;
    private float conflictRemaining;

    public bool IsRebinding => operation != null;

    private void Awake()
    {
        // 활성화하지 않는 편집용 인스턴스 (게임 입력과 겹치지 않음)
        actions = new PlayerAction();
        InputBindings.Register(actions.asset);

        foreach (var row in rebindRows)
        {
            row.Bind(actions.asset);
            row.Clicked += StartRebind;
        }
    }

    private void OnDestroy()
    {
        CancelRebind();
        InputBindings.Unregister(actions.asset);
        actions.Dispose();
    }

    private void Update()
    {
        if (conflictRow == null) return;

        conflictRemaining -= Time.unscaledDeltaTime;
        if (conflictRemaining <= 0f)
        {
            conflictRow.Refresh();
            conflictRow = null;
        }
    }

    public override void Refresh()
    {
        foreach (var row in rebindRows)
        {
            row.Refresh();
        }
    }

    public override void ResetToDefault()
    {
        CancelRebind();
        InputBindings.ResetAll();
        Refresh();
    }

    public override bool HandleBack()
    {
        // 대기 중 누른 Esc는 대기 취소로 이미 처리되었으므로 같은 입력으로 설정 창까지 닫지 않음
        if (Time.frameCount - lastRebindFrame <= 1) return true;
        if (!IsRebinding) return false;

        CancelRebind();
        return true;
    }

    public override void OnHide()
    {
        CancelRebind();
    }

    private void StartRebind(RebindRow row)
    {
        if (IsRebinding || Time.frameCount - lastRebindFrame <= 1 || row.BindingIndex < 0) return;

        ClearConflict();
        rebindingRow = row;
        row.ShowWaiting();

        // 선택이 다른 항목으로 옮겨가지 않도록 대기 중에는 UI 이동을 막음
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;

        string previousOverride = row.Action.bindings[row.BindingIndex].overridePath;

        operation = row.Action.PerformInteractiveRebinding(row.BindingIndex)
            .WithExpectedControlType("Button")
            .WithControlsHavingToMatchPath("<Keyboard>")
            .WithControlsHavingToMatchPath("<Mouse>")
            .WithControlsExcluding("<Mouse>/press")
            .WithCancelingThrough("<Keyboard>/escape")
            .WithMatchingEventsBeingSuppressed()
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op => OnRebindComplete(row, previousOverride))
            .OnCancel(op => FinishRebind(row))
            .Start();
    }

    private void OnRebindComplete(RebindRow row, string previousOverride)
    {
        string oldPath = string.IsNullOrEmpty(previousOverride) ? row.Action.bindings[row.BindingIndex].path : previousOverride;
        string newPath = row.Action.bindings[row.BindingIndex].effectivePath;

        if (TryResolveConflict(row, oldPath, newPath))
        {
            InputBindings.Save(actions.asset);
        }
        else
        {
            // 되돌리고 잠깐 안내 문구 표시
            if (string.IsNullOrEmpty(previousOverride)) row.Action.RemoveBindingOverride(row.BindingIndex);
            else row.Action.ApplyBindingOverride(row.BindingIndex, previousOverride);
        }

        FinishRebind(row);

        if (conflictRow == row) row.ShowConflict(conflictText);
    }

    // 같은 키를 쓰는 항목이 이 화면에 있으면 이전 키와 맞바꿈. 화면에 없는 바인딩과 겹치면 false
    private bool TryResolveConflict(RebindRow row, string oldPath, string newPath)
    {
        foreach (var other in rebindRows)
        {
            if (other == row || other.BindingIndex < 0) continue;
            if (other.Action.bindings[other.BindingIndex].effectivePath != newPath) continue;

            other.Action.ApplyBindingOverride(other.BindingIndex, oldPath);
            other.Refresh();
            return true;
        }

        foreach (var map in actions.asset.actionMaps)
        {
            foreach (var binding in map.bindings)
            {
                if (binding.isComposite || binding.id == row.Action.bindings[row.BindingIndex].id) continue;
                if (IsRebindable(binding)) continue;
                if (binding.effectivePath != newPath) continue;

                conflictRow = row;
                conflictRemaining = conflictShowSeconds;
                return false;
            }
        }

        return true;
    }

    private bool IsRebindable(InputBinding binding)
    {
        foreach (var other in rebindRows)
        {
            if (other.BindingIndex >= 0 && other.Action.bindings[other.BindingIndex].id == binding.id) return true;
        }
        return false;
    }

    private void FinishRebind(RebindRow row)
    {
        operation?.Dispose();
        operation = null;
        rebindingRow = null;
        lastRebindFrame = Time.frameCount;

        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
        row.Refresh();
    }

    private void CancelRebind()
    {
        if (operation == null) return;

        // Cancel은 OnCancel 콜백(FinishRebind)을 호출함
        operation.Cancel();
        if (rebindingRow != null) FinishRebind(rebindingRow);
    }

    private void ClearConflict()
    {
        if (conflictRow == null) return;

        conflictRow.Refresh();
        conflictRow = null;
    }
}
