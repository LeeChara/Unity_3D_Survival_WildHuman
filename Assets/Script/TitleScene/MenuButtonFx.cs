using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 메뉴 버튼 강조 표시. 마우스를 올리면 그 버튼을 선택해서, 마우스·키보드 모두 "선택된 버튼"만 강조됨
// Button의 Transition은 None으로 두고 이 스크립트가 색·크기·표시를 담당
public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
{
    [Tooltip("판자 조각 등 색을 바꿀 이미지들")]
    [SerializeField] private Graphic[] tintTargets;
    [SerializeField] private TMP_Text label;
    [Tooltip("선택 시에만 켜지는 표시 (양옆 화살표 등)")]
    [SerializeField] private GameObject[] markers;

    // 버텍스 색은 1을 넘을 수 없으므로 평소 색을 어둡게 두고 선택 시 원래 색으로 밝힘
    [SerializeField] private Color normalTint = new(0.82f, 0.82f, 0.82f, 1f);
    [SerializeField] private Color highlightTint = Color.white;
    [SerializeField] private Color normalLabel = new(0.95f, 0.88f, 0.74f, 1f);
    [SerializeField] private Color highlightLabel = Color.white;
    [SerializeField] private float highlightScale = 1.06f;
    [SerializeField] private float scaleSpeed = 14f;

    private bool highlighted;

    private void Awake()
    {
        Apply(false);
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        // 타이틀 진입 시 timeScale이 0일 가능성에 대비해 실제 시간 기준
        float target = highlighted ? highlightScale : 1f;
        float scale = Mathf.Lerp(transform.localScale.x, target, scaleSpeed * Time.unscaledDeltaTime);
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnSelect(BaseEventData eventData) => Apply(true);
    public void OnDeselect(BaseEventData eventData) => Apply(false);

    private void Apply(bool on)
    {
        highlighted = on;

        foreach (var target in tintTargets)
        {
            target.color = on ? highlightTint : normalTint;
        }
        if (label != null) label.color = on ? highlightLabel : normalLabel;
        foreach (var marker in markers)
        {
            marker.SetActive(on);
        }
    }
}
