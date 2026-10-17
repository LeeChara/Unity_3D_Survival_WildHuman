using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 모든 씬의 UI 버튼 소리 (마우스를 올리면 hover, 누르면 click, 누를 수 없는 버튼을 누르면 deny)
// 버튼마다 컴포넌트를 붙이지 않도록 커서 아래의 Selectable을 직접 찾음 (나중에 만들어지는 버튼도 그대로 적용)
// 키보드·패드로 선택이 옮겨갈 때도 hover 소리
public class UiSfx : MonoBehaviour
{
    private readonly List<RaycastResult> results = new();
    private Selectable hovered;
    private GameObject lastSelected;
    private Vector2 lastPointer = new(-1f, -1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var host = new GameObject("UiSfx");
        DontDestroyOnLoad(host);
        host.AddComponent<UiSfx>();
    }

    private void Update()
    {
        EventSystem eventSystem = EventSystem.current;
        SfxSetting setting = SfxSetting.Instance;
        if (eventSystem == null || setting == null) return;

        Mouse mouse = Mouse.current;
        bool pointerMoved = false;
        if (mouse != null)
        {
            Vector2 position = mouse.position.ReadValue();
            bool pressed = mouse.leftButton.wasPressedThisFrame;
            pointerMoved = position != lastPointer;
            lastPointer = position;

            if (pointerMoved || pressed)
            {
                Selectable under = FindSelectable(eventSystem, position);
                if (under != hovered)
                {
                    hovered = under;
                    if (under != null && under.IsInteractable()) Sfx.PlayUI(setting.uiHover);
                }
                if (pressed && under != null) Sfx.PlayUI(under.IsInteractable() ? setting.uiClick : setting.uiDeny);
            }
        }

        // 마우스로 올려서 선택된 경우(SelectOnHover)는 위에서 이미 소리를 냈으므로 건너뜀
        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected != lastSelected)
        {
            bool byMouse = pointerMoved || (hovered != null && hovered.gameObject == selected);
            if (selected != null && !byMouse && lastSelected != null) Sfx.PlayUI(setting.uiHover);
            lastSelected = selected;
        }
    }

    // 커서 아래 가장 위의 UI에서 위로 올라가며 찾은 Selectable (입력칸·슬라이더 포함)
    private Selectable FindSelectable(EventSystem eventSystem, Vector2 position)
    {
        var data = new PointerEventData(eventSystem) { position = position };
        results.Clear();
        eventSystem.RaycastAll(data, results);
        if (results.Count == 0) return null;
        return results[0].gameObject.GetComponentInParent<Selectable>();
    }
}
