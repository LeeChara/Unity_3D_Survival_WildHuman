using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// 월드 목록의 슬롯 하나 (표시만 담당하고, 선택 상태는 WorldSelectUI가 관리)
// 클릭·키보드 이동으로 선택되면 Selected, 더블클릭·Enter면 Submitted
public class WorldSlotUI : MonoBehaviour, ISelectHandler, ISubmitHandler, IPointerClickHandler
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text nameShadow;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private TMP_Text dateText;
    [Tooltip("선택된 슬롯 강조")]
    [SerializeField] private GameObject highlight;

    public WorldMeta Meta { get; private set; }

    public event Action<WorldSlotUI> Selected;
    public event Action<WorldSlotUI> Submitted;

    public void Bind(WorldMeta meta, WorldGenConfig config)
    {
        Meta = meta;
        nameText.text = meta.name;
        nameShadow.text = meta.name;

        string mapSize = config.GetMapSize(meta.settings.mapSize).displayName;
        string difficulty = config.DifficultyName(meta.settings.difficulty);
        infoText.text = $"{meta.dayCount}일차 · {mapSize} · {difficulty}";

        DateTime lastPlayed = new DateTime(meta.lastPlayedAt, DateTimeKind.Utc).ToLocalTime();
        dateText.text = $"마지막 플레이 {lastPlayed:MM-dd HH:mm} · {FormatPlayTime(meta.playTime)}";

        SetHighlighted(false);
    }

    public void SetHighlighted(bool value)
    {
        highlight.SetActive(value);
    }

    public void OnSelect(BaseEventData eventData) => Selected?.Invoke(this);

    public void OnSubmit(BaseEventData eventData) => Submitted?.Invoke(this);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2) Submitted?.Invoke(this);
    }

    private static string FormatPlayTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        if (minutes < 1) return "1분 미만";
        if (minutes < 60) return $"{minutes}분";
        return $"{minutes / 60}시간 {minutes % 60}분";
    }
}
