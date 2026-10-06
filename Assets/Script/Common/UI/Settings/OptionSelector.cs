using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ◀ 값 ▶ 형태로 목록 중 하나를 고르는 설정 항목. 키보드 좌우 또는 양옆 화살표 클릭으로 바꿈
// 위아래 이동은 SettingsUI가 지정한 내비게이션을 따름
public class OptionSelector : Selectable
{
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [Tooltip("끝에서 반대쪽 끝으로 넘어갈지 여부")]
    [SerializeField] private bool wrap = true;

    public event Action<int> ValueChanged;

    public int Index { get; private set; }

    private readonly List<string> options = new();

    protected override void Awake()
    {
        base.Awake();
        if (!Application.isPlaying) return;

        if (previousButton != null) previousButton.onClick.AddListener(() => Step(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
    }

    public void SetOptions(IEnumerable<string> values, int index)
    {
        options.Clear();
        options.AddRange(values);
        SetIndexWithoutNotify(index);
    }

    public void SetIndexWithoutNotify(int index)
    {
        Index = options.Count > 0 ? Mathf.Clamp(index, 0, options.Count - 1) : 0;
        if (valueText != null) valueText.text = options.Count > 0 ? options[Index] : string.Empty;
    }

    public override void OnMove(AxisEventData eventData)
    {
        switch (eventData.moveDir)
        {
            case MoveDirection.Left:
                Step(-1);
                eventData.Use();
                break;
            case MoveDirection.Right:
                Step(1);
                eventData.Use();
                break;
            default:
                base.OnMove(eventData);
                break;
        }
    }

    private void Step(int direction)
    {
        if (!IsInteractable() || options.Count == 0) return;

        // 화살표를 클릭해도 이 항목이 선택된 상태로 남게 함
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);

        int next = Index + direction;
        if (wrap) next = (next + options.Count) % options.Count;
        else next = Mathf.Clamp(next, 0, options.Count - 1);
        if (next == Index) return;

        SetIndexWithoutNotify(next);
        ValueChanged?.Invoke(Index);
    }
}
