using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 확인창 (삭제 확인 등). 취소 문구를 주지 않으면 버튼 하나짜리 안내창
public class ConfirmDialogUI : MonoBehaviour
{
    [SerializeField] private TMP_Text message;
    [SerializeField] private Button confirmButton;
    [Tooltip("확인 버튼의 글자와 그림자")]
    [SerializeField] private TMP_Text[] confirmLabels;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text[] cancelLabels;

    public bool IsOpen => gameObject.activeSelf;

    public event Action Closed;

    private Action onConfirm;
    private Vector2 confirmPosition;
    private bool initialized;

    // 비활성으로 시작하는 창이라 Awake는 처음 Show로 켜질 때에야 실행됨 → Show에서 먼저 초기화
    private void Initialize()
    {
        if (initialized) return;
        initialized = true;

        confirmPosition = ((RectTransform)confirmButton.transform).anchoredPosition;
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Close);

        // 창 뒤의 버튼으로 키보드 포커스가 빠져나가지 않도록 두 버튼끼리만 이동
        confirmButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = cancelButton, selectOnLeft = cancelButton };
        cancelButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = confirmButton, selectOnRight = confirmButton };
    }

    public void Show(string text, string confirmText, Action onConfirm, string cancelText = null)
    {
        Initialize();
        this.onConfirm = onConfirm;
        message.text = text;
        SetLabels(confirmLabels, confirmText);

        bool hasCancel = cancelText != null;
        cancelButton.gameObject.SetActive(hasCancel);
        if (hasCancel) SetLabels(cancelLabels, cancelText);
        // 버튼 하나면 가운데로
        ((RectTransform)confirmButton.transform).anchoredPosition = hasCancel ? confirmPosition : new Vector2(0f, confirmPosition.y);

        gameObject.SetActive(true);
        // 되돌릴 수 없는 동작이 있는 창은 취소에 먼저 포커스
        Select(hasCancel ? cancelButton : confirmButton);
    }

    public void Close()
    {
        if (!IsOpen) return;

        gameObject.SetActive(false);
        Closed?.Invoke();
    }

    private void Confirm()
    {
        Action action = onConfirm;
        Close();
        action?.Invoke();
    }

    private static void SetLabels(TMP_Text[] labels, string text)
    {
        foreach (var label in labels)
        {
            label.text = text;
        }
    }

    private static void Select(Selectable target)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target.gameObject);
    }
}
