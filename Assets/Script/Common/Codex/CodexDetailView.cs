using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도감 오른쪽 상세 영역. 고정된 머리말(아이콘·이름·분류·설명) 아래에 템플릿을 복제해서 내용을 쌓음
// 무엇을 쌓을지는 CodexDetailBuilder가 정하고, 이 스크립트는 배치만 담당
public class CodexDetailView : MonoBehaviour
{
    [Header("머리말")]
    [SerializeField] private Image headerIcon;
    [SerializeField] private TMP_Text headerName;
    [SerializeField] private TMP_Text headerSub;
    [SerializeField] private TMP_Text headerDescription;

    [Header("내용")]
    [SerializeField] private ScrollRect scroll;
    [Tooltip("머리말 아래에 템플릿 복제본이 쌓이는 곳 (VerticalLayoutGroup)")]
    [SerializeField] private RectTransform content;

    [Header("템플릿 (비활성 상태로 둘 것)")]
    [SerializeField] private TMP_Text sectionTemplate;
    [SerializeField] private TMP_Text textTemplate;
    [Tooltip("조합법 한 줄처럼 칸을 가로로 늘어놓는 줄 (HorizontalLayoutGroup)")]
    [SerializeField] private RectTransform rowTemplate;
    [Tooltip("가로 줄 안에 들어가는 작은 칸 (아이콘 + 개수)")]
    [SerializeField] private CodexLink chipTemplate;
    [Tooltip("가로 줄 안에 들어가는 글자 (→, + 등)")]
    [SerializeField] private TMP_Text inlineTextTemplate;
    [Tooltip("세로 목록의 한 줄 (아이콘 + 설명)")]
    [SerializeField] private CodexLink lineTemplate;

    private readonly List<GameObject> spawned = new();

    public void Clear()
    {
        // Destroy는 프레임 끝에 처리되므로 먼저 꺼서 바로 다음 레이아웃 계산에서 빠지게 함
        foreach (GameObject item in spawned)
        {
            item.SetActive(false);
            Destroy(item);
        }
        spawned.Clear();
    }

    public void SetHeader(Sprite icon, string name, string sub, string description)
    {
        headerIcon.sprite = icon;
        headerIcon.enabled = icon != null;
        headerName.text = name;
        headerSub.text = sub;
        headerDescription.text = description;
        headerDescription.gameObject.SetActive(!string.IsNullOrEmpty(description));
    }

    public void Section(string title)
    {
        Spawn(sectionTemplate, content).text = title;
    }

    public void Text(string text)
    {
        Spawn(textTemplate, content).text = text;
    }

    public RectTransform Row()
    {
        return Spawn(rowTemplate, content);
    }

    public void Chip(RectTransform row, Sprite icon, string text, Action onClick)
    {
        Spawn(chipTemplate, row).Set(icon, text, onClick);
    }

    public void InlineText(RectTransform row, string text)
    {
        Spawn(inlineTextTemplate, row).text = text;
    }

    public void Line(Sprite icon, string text, Action onClick)
    {
        Spawn(lineTemplate, content).Set(icon, text, onClick);
    }

    // 내용을 다 채운 뒤 맨 위로 스크롤
    public void ScrollToTop()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.verticalNormalizedPosition = 1f;
    }

    private T Spawn<T>(T template, Transform parent) where T : Component
    {
        T instance = Instantiate(template, parent);
        instance.gameObject.SetActive(true);
        spawned.Add(instance.gameObject);
        return instance;
    }
}
