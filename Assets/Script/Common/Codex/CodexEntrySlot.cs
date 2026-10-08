using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도감 목록의 한 칸 (아이콘 + 이름). 선택되면 배경을 바꿔 강조
public class CodexEntrySlot : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;

    public CodexEntry Entry { get; private set; }

    public void Set(CodexEntry entry, Action<CodexEntry> onClick)
    {
        Entry = entry;
        icon.sprite = entry.icon;
        icon.enabled = entry.icon != null;
        nameText.text = entry.name;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick(entry));
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        background.sprite = selected ? selectedSprite : normalSprite;
    }
}
