using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 새 월드(맵 설정) 창. 입력값을 정리해 Created로 넘기고, 실제 생성은 WorldSelectUI가 처리
public class CreateWorldUI : MonoBehaviour
{
    [SerializeField] private WorldGenConfig config;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField seedInput;
    [SerializeField] private OptionSelector mapSizeSelector;
    [SerializeField] private OptionSelector difficultySelector;
    [SerializeField] private Button createButton;
    [SerializeField] private Button cancelButton;

    [Header("기본값")]
    [SerializeField] private string defaultName = "새로운 월드";
    [SerializeField] private string defaultMapSize = "large";
    [SerializeField] private string defaultDifficulty = "normal";

    public bool IsOpen => gameObject.activeSelf;
    // 글자 입력 중에는 Esc를 입력 취소로만 쓰도록 WorldSelectUI가 확인
    public bool IsEditingText => nameInput.isFocused || seedInput.isFocused;

    public event Action<string, WorldGenSettings> Created;
    public event Action Closed;

    private readonly HashSet<string> existingNames = new();

    private void Awake()
    {
        createButton.onClick.AddListener(Create);
        cancelButton.onClick.AddListener(Close);
        SetupNavigation();
    }

    public void Open(IEnumerable<string> names)
    {
        existingNames.Clear();
        existingNames.UnionWith(names);

        nameInput.text = string.Empty;
        seedInput.text = string.Empty;
        mapSizeSelector.SetOptions(config.mapSizes.Select(option => option.displayName), config.IndexOfMapSize(defaultMapSize));
        difficultySelector.SetOptions(config.difficulties.Select(option => option.displayName), config.IndexOfDifficulty(defaultDifficulty));

        gameObject.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(nameInput.gameObject);
    }

    public void Close()
    {
        if (!IsOpen) return;

        gameObject.SetActive(false);
        Closed?.Invoke();
    }

    private void Create()
    {
        var settings = new WorldGenSettings
        {
            seed = WorldCreator.ParseSeed(seedInput.text),
            mapSize = config.mapSizes[mapSizeSelector.Index].id,
            difficulty = config.difficulties[difficultySelector.Index].id,
        };
        Created?.Invoke(UniqueName(nameInput.text), settings);
    }

    // 비었으면 기본 이름, 이미 있으면 " (2)", " (3)" ...
    private string UniqueName(string input)
    {
        string baseName = string.IsNullOrWhiteSpace(input) ? defaultName : input.Trim();
        if (!existingNames.Contains(baseName)) return baseName;

        for (int n = 2; ; n++)
        {
            string candidate = $"{baseName} ({n})";
            if (!existingNames.Contains(candidate)) return candidate;
        }
    }

    // 창 뒤의 목록으로 키보드 포커스가 빠져나가지 않도록 창 안에서만 위아래로 이동
    private void SetupNavigation()
    {
        Selectable[] column = { nameInput, seedInput, mapSizeSelector, difficultySelector, createButton };
        for (int i = 0; i < column.Length; i++)
        {
            Navigation navigation = column[i].navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = i > 0 ? column[i - 1] : null;
            navigation.selectOnDown = i < column.Length - 1 ? column[i + 1] : null;
            column[i].navigation = navigation;
        }

        Navigation create = createButton.navigation;
        create.selectOnRight = cancelButton;
        createButton.navigation = create;

        cancelButton.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = difficultySelector,
            selectOnLeft = createButton,
        };
    }
}
