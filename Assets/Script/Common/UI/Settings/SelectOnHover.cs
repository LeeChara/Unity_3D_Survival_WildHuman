using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 마우스를 올리면 선택해서, 마우스·키보드 모두 "선택된 항목"만 강조되게 함 (MenuButtonFx와 같은 방식)
// 슬라이더 줄처럼 선택 대상이 줄의 일부일 때는 줄 전체에 두고 target을 지정 (이름 부분을 눌러도 선택이 풀리지 않음)
public class SelectOnHover : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
{
    [Tooltip("비워 두면 같은 오브젝트의 Selectable")]
    [SerializeField] private Selectable target;

    private void Awake()
    {
        if (target == null) target = GetComponent<Selectable>();
    }

    public void OnPointerEnter(PointerEventData eventData) => Select();

    // 선택할 수 없는 곳을 누르면 EventSystem이 선택을 풀기 때문에 다시 선택
    public void OnPointerDown(PointerEventData eventData) => Select();

    private void Select()
    {
        if (EventSystem.current != null && target != null && target.IsInteractable())
        {
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
    }
}
