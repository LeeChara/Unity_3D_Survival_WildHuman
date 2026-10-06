using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 키 설정 한 줄. 누르면 ControlsSettingsPage가 키 입력 대기를 시작함
// 바꿀 바인딩은 액션 이름 + (2D Vector 같은 복합 바인딩이면) 부분 이름으로 지정
[RequireComponent(typeof(Button))]
public class RebindRow : MonoBehaviour
{
    [SerializeField] private string actionName;
    [Tooltip("복합 바인딩의 부분 이름 (Move의 up/down/left/right). 일반 바인딩이면 비워 둠")]
    [SerializeField] private string compositePart;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private string waitingText = "키를 누르세요";

    public event Action<RebindRow> Clicked;

    public InputAction Action { get; private set; }
    public int BindingIndex { get; private set; } = -1;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => Clicked?.Invoke(this));
    }

    // 설정 화면 전용 입력 에셋에서 바꿀 바인딩을 찾음
    public void Bind(InputActionAsset asset)
    {
        Action = asset.FindAction(actionName, throwIfNotFound: true);
        BindingIndex = Action.bindings.IndexOf(b =>
            string.IsNullOrEmpty(compositePart)
                ? !b.isComposite && !b.isPartOfComposite
                : b.isPartOfComposite && string.Equals(b.name, compositePart, StringComparison.OrdinalIgnoreCase));

        if (BindingIndex < 0) Debug.LogError($"{actionName}/{compositePart} 바인딩을 찾을 수 없음", this);
    }

    public void ShowWaiting()
    {
        keyText.text = waitingText;
    }

    public void ShowConflict(string message)
    {
        keyText.text = message;
    }

    public void Refresh()
    {
        if (BindingIndex < 0) return;
        keyText.text = DisplayName(Action.bindings[BindingIndex].effectivePath);
    }

    private static string DisplayName(string path)
    {
        switch (path)
        {
            case "<Mouse>/leftButton": return "마우스 왼쪽";
            case "<Mouse>/rightButton": return "마우스 오른쪽";
            case "<Mouse>/middleButton": return "마우스 가운데";
            case "<Mouse>/backButton": return "마우스 뒤로";
            case "<Mouse>/forwardButton": return "마우스 앞으로";
        }
        return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
    }
}
