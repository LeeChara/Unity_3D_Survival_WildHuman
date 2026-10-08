using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도감 상세에 들어가는 "아이콘 + 글자" 한 덩어리 (조합법의 재료 칸, 드랍·서식지 목록의 한 줄)
// 도감에 있는 대상이면 클릭해서 그 항목으로 이동
public class CodexLink : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [Tooltip("이동할 수 있는 링크일 때 글자색")]
    [SerializeField] private Color linkColor = new(1f, 0.88f, 0.54f, 1f);
    [SerializeField] private Color plainColor = new(0.95f, 0.88f, 0.74f, 1f);

    // onClick이 null이면 클릭할 수 없는 일반 표시
    public void Set(Sprite sprite, string text, Action onClick)
    {
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        label.text = text;
        label.color = onClick != null ? linkColor : plainColor;

        button.onClick.RemoveAllListeners();
        button.interactable = onClick != null;
        if (onClick != null) button.onClick.AddListener(() => onClick());
    }
}
