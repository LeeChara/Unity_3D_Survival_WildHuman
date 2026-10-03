using UnityEngine;
using UnityEngine.EventSystems;

// UI 위에 마우스를 올리면 지정된 글자를 HoverTextUI로 표시 (체력·허기 바, 시계 등에 붙여서 사용)
// 표시할 글자는 이 UI를 그리는 스크립트가 SetText로 넣어 줌
// 이벤트는 자식의 Raycast Target이 켜진 Graphic에서 부모로 전달되므로, 자식 Image가 있으면 루트에 붙여도 됨
public class HoverTextTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private string text;
    private bool hovered;

    // 마우스를 올린 채 값이 바뀌면 바로 반영
    public void SetText(string value)
    {
        if (value == text) return;

        text = value;
        if (hovered) Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        Refresh();
    }

    // 비활성화되면 PointerExit가 오지 않으므로 직접 정리
    private void OnDisable()
    {
        hovered = false;
        Refresh();
    }

    private void Refresh()
    {
        HoverTextUI hoverText = HoverTextUI.Instance;
        if (hoverText == null) return;

        if (hovered && !string.IsNullOrEmpty(text)) hoverText.Show(this, text);
        else hoverText.Hide(this);
    }
}
