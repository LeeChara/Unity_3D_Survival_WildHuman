using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 도감 창. 탭(분류) 전환, 왼쪽 목록 생성, 고른 항목의 상세 표시를 담당
// Esc 입력은 여는 쪽(MainMenuUI)이 받아서 Close()를 호출
public class CodexUI : MonoBehaviour
{
    [Serializable]
    private class Tab
    {
        public CodexCategory category;
        public Button button;
        [Tooltip("선택되지 않은 탭을 흐리게 표시")]
        public CanvasGroup group;
    }

    [SerializeField] private CodexDatabase database;
    // 이 스크립트는 항상 활성 상태인 부모에 두고, 패널만 켜고 끔
    [SerializeField] private GameObject panel;
    [SerializeField] private Tab[] tabs;
    [SerializeField] private Button closeButton;
    [SerializeField] private float inactiveTabAlpha = 0.55f;
    [Tooltip("id·내부 수치 등 개발용 정보 표시 (출시용 도감에서는 끔)")]
    [SerializeField] private bool showDevInfo = true;

    [Header("목록")]
    [SerializeField] private ScrollRect listScroll;
    [SerializeField] private RectTransform listContent;
    [Tooltip("비활성 상태로 둘 것")]
    [SerializeField] private CodexEntrySlot slotTemplate;

    [Header("상세")]
    [SerializeField] private CodexDetailView detailView;

    public bool IsOpen => panel.activeSelf;
    public event Action Closed;

    private readonly List<CodexEntrySlot> slots = new();
    private CodexDetailBuilder builder;
    private CodexCategory currentCategory;
    // 탭마다 마지막으로 본 항목 (탭을 오가도 유지)
    private readonly Dictionary<CodexCategory, CodexEntry> lastSelected = new();
    private CodexEntry selected;

    private void Awake()
    {
        builder = new CodexDetailBuilder(database, detailView, Navigate) { ShowDevInfo = showDevInfo };

        foreach (Tab tab in tabs)
        {
            CodexCategory category = tab.category;
            tab.button.onClick.AddListener(() => ShowCategory(category));
        }
        closeButton.onClick.AddListener(Close);

        slotTemplate.gameObject.SetActive(false);
        panel.SetActive(false);
    }

    public void Open()
    {
        if (IsOpen) return;

        panel.SetActive(true);
        ShowCategory(currentCategory);
    }

    public void Close()
    {
        if (!IsOpen) return;

        panel.SetActive(false);
        Closed?.Invoke();
    }

    private void ShowCategory(CodexCategory category)
    {
        currentCategory = category;
        foreach (Tab tab in tabs)
        {
            tab.group.alpha = tab.category == category ? 1f : inactiveTabAlpha;
        }

        RebuildList(database.GetEntries(category));
        listScroll.verticalNormalizedPosition = 1f;

        lastSelected.TryGetValue(category, out CodexEntry entry);
        if (entry == null && slots.Count > 0) entry = slots[0].Entry;
        if (entry != null) Select(entry);
        else detailView.Clear();
    }

    private void RebuildList(IReadOnlyList<CodexEntry> entries)
    {
        // 슬롯은 재사용하고 모자라면 복제
        while (slots.Count < entries.Count)
        {
            slots.Add(Instantiate(slotTemplate, listContent));
        }

        for (int i = 0; i < slots.Count; i++)
        {
            bool used = i < entries.Count;
            slots[i].gameObject.SetActive(used);
            if (used) slots[i].Set(entries[i], Select);
        }
    }

    private void Select(CodexEntry entry)
    {
        selected = entry;
        lastSelected[entry.category] = entry;

        foreach (CodexEntrySlot slot in slots)
        {
            if (slot.gameObject.activeSelf) slot.SetSelected(slot.Entry == entry);
        }

        builder.Build(entry);
    }

    // 상세 안의 링크를 눌렀을 때: 그 분류의 탭으로 바꾸고 항목을 고른 뒤 목록에서 보이게 스크롤
    private void Navigate(CodexEntry entry)
    {
        if (entry == selected) return;

        lastSelected[entry.category] = entry;
        if (entry.category != currentCategory) ShowCategory(entry.category);
        else Select(entry);

        ScrollListTo(entry);
    }

    private void ScrollListTo(CodexEntry entry)
    {
        CodexEntrySlot slot = slots.Find(s => s.gameObject.activeSelf && s.Entry == entry);
        if (slot == null) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
        var viewport = (RectTransform)listScroll.viewport;
        var slotRect = (RectTransform)slot.transform;

        // 콘텐츠 위쪽 기준으로 슬롯의 위·아래 위치를 구해서, 뷰포트 밖이면 그만큼만 이동
        float slotTop = -slotRect.anchoredPosition.y - slotRect.rect.height * (1f - slotRect.pivot.y);
        float slotBottom = slotTop + slotRect.rect.height;
        float viewHeight = viewport.rect.height;
        float scrollY = listContent.anchoredPosition.y;

        if (slotTop < scrollY) scrollY = slotTop;
        else if (slotBottom > scrollY + viewHeight) scrollY = slotBottom - viewHeight;

        float maxScroll = Mathf.Max(0f, listContent.rect.height - viewHeight);
        listContent.anchoredPosition = new Vector2(listContent.anchoredPosition.x, Mathf.Clamp(scrollY, 0f, maxScroll));
    }
}
