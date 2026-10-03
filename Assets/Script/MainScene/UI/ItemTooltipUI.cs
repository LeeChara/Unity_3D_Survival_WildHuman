using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 아이템 위에 마우스를 올렸을 때 커서를 따라다니는 정보 창 (이름 / 설명 / 스탯)
// 슬롯(ItemTooltipTrigger)과 땅의 아이템(WorldItemHover)이 요청한 쪽(owner)을 기록해서,
// 다른 쪽의 Hide 요청이 지금 표시 중인 툴팁을 끄지 않게 함
public class ItemTooltipUI : MonoBehaviour
{
    // 이 스크립트는 항상 활성 상태인 부모에 두고, 패널만 켜고 끔 (Canvas 맨 아래에 두어 가장 위에 그려지게 함)
    [Tooltip("배경 + 글자. VerticalLayoutGroup과 ContentSizeFitter로 글자에 맞춰 크기가 바뀌게 구성")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text statText;

    [Tooltip("{0}에 이름, {1}에 개수가 들어감 (개수가 2개 이상일 때만 사용)")]
    [SerializeField] private string countFormat = "{0} ({1})";
    [Tooltip("커서로부터 떨어진 거리 (Canvas 단위, 오른쪽 아래 방향)")]
    [SerializeField] private Vector2 cursorOffset = new(16f, -16f);

    public static ItemTooltipUI Instance { get; private set; }

    private Canvas canvas;
    private object owner;
    private ItemData item;
    private int count;
    // 드래그 중에는 표시하지 않음 (요청 내용은 기억해 두었다가 끝나면 표시)
    private bool suppressed;

    private void Awake()
    {
        Instance = this;
        canvas = GetComponentInParent<Canvas>();

        // 툴팁이 커서 아래의 슬롯을 가리면 PointerExit가 발생해 깜빡이므로 레이캐스트를 막지 않게 함
        if (!panel.TryGetComponent(out CanvasGroup group)) group = panel.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        panel.gameObject.SetActive(false);
    }

    // count가 1 이하면 이름 뒤에 개수를 붙이지 않음
    public void Show(object requester, ItemData data, int amount)
    {
        owner = requester;
        item = data;
        count = amount;
        Redraw();
    }

    // 지금 표시 중인 요청을 한 쪽만 끌 수 있음
    public void Hide(object requester)
    {
        if (owner != requester) return;

        owner = null;
        item = null;
        Redraw();
    }

    public void SetSuppressed(bool value)
    {
        suppressed = value;
        Redraw();
    }

    private void Redraw()
    {
        bool show = owner != null && item != null && !suppressed;
        panel.gameObject.SetActive(show);
        if (!show) return;

        nameText.text = count > 1 ? string.Format(countFormat, item.displayName, count) : item.displayName;
        SetLine(descriptionText, item.description);
        SetLine(statText, item.GetStatText());

        // 크기가 바로 반영되어야 이번 프레임에 화면 끝 판정이 맞음
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        CursorFollower.Place(panel, canvas, cursorOffset);
    }

    private static void SetLine(TMP_Text text, string value)
    {
        bool hasValue = !string.IsNullOrEmpty(value);
        text.gameObject.SetActive(hasValue);
        if (hasValue) text.text = value;
    }

    private void LateUpdate()
    {
        if (panel.gameObject.activeSelf) CursorFollower.Place(panel, canvas, cursorOffset);
    }
}
