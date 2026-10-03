using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 배경 없이 커서 옆에 뜨는 흰 글자 + 검은 픽셀 외곽선 (체력·허기 바, 시계, 몬스터·Prop 등)
// 요청한 쪽(owner)을 기록해서, 다른 쪽의 Hide 요청이 지금 표시 중인 글자를 끄지 않게 함
public class HoverTextUI : MonoBehaviour
{
    // 이 스크립트는 항상 활성 상태인 부모에 두고, 패널만 켜고 끔 (Canvas 맨 아래에 두어 가장 위에 그려지게 함)
    [Tooltip("글자에 맞춰 크기가 바뀌는 영역 (ContentSizeFitter)")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text text;
    [Tooltip("글자 뒤에 상하좌우 1px씩 어긋나게 깐 검은 글자 (픽셀 외곽선)")]
    [SerializeField] private TMP_Text[] outlines;
    [Tooltip("커서로부터 떨어진 거리 (Canvas 단위, 오른쪽 아래 방향)")]
    [SerializeField] private Vector2 cursorOffset = new(16f, -16f);

    public static HoverTextUI Instance { get; private set; }

    private Canvas canvas;
    private object owner;
    private string shown;

    private void Awake()
    {
        Instance = this;
        canvas = GetComponentInParent<Canvas>();

        // 글자가 커서 아래의 UI를 가리면 PointerExit가 발생해 깜빡이므로 레이캐스트를 막지 않게 함
        if (!panel.TryGetComponent(out CanvasGroup group)) group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        panel.gameObject.SetActive(false);
    }

    public void Show(object requester, string value)
    {
        owner = requester;
        panel.gameObject.SetActive(true);

        // 같은 글자면 다시 그리지 않음 (매 프레임 호출되는 경우 대비)
        if (value != shown)
        {
            shown = value;
            text.text = value;
            foreach (var outline in outlines)
            {
                outline.text = value;
            }

            // 크기가 바로 반영되어야 이번 프레임에 화면 끝 판정이 맞음
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        CursorFollower.Place(panel, canvas, cursorOffset);
    }

    // 지금 표시 중인 요청을 한 쪽만 끌 수 있음
    public void Hide(object requester)
    {
        if (owner != requester) return;

        owner = null;
        panel.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (panel.gameObject.activeSelf) CursorFollower.Place(panel, canvas, cursorOffset);
    }
}
