using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 월드 선택 화면: 세이브 목록 표시, 플레이·새 월드·삭제, 페이드 후 씬 이동
// Esc는 확인창 → 새 월드 창 → 타이틀 순서로 뒤로 감
public class WorldSelectUI : MonoBehaviour
{
    [SerializeField] private WorldGenConfig config;
    [SerializeField] private ScreenFader fader;

    [Header("목록")]
    [SerializeField] private Transform listContent;
    [SerializeField] private WorldSlotUI slotPrefab;
    [Tooltip("월드가 하나도 없을 때 보여 줄 안내")]
    [SerializeField] private GameObject emptyText;

    [Header("버튼 (OnClick은 이 컴포넌트의 public 메서드에 연결)")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button newButton;
    [SerializeField] private Button deleteButton;

    [Header("창")]
    [SerializeField] private CreateWorldUI createWorldUI;
    [SerializeField] private ConfirmDialogUI confirmDialog;

    private readonly List<WorldSlotUI> slots = new();
    private WorldSlotUI selected;
    private bool isLeaving;
    // Esc 입력 (일시정지 메뉴·타이틀과 같은 System 맵)
    private PlayerAction actions;

    private void Awake()
    {
        actions = new PlayerAction();
        InputBindings.Register(actions.asset);
    }

    private void OnEnable()
    {
        actions.System.Pause.performed += OnBackPerformed;
        actions.System.Enable();
        createWorldUI.Created += OnWorldCreated;
        createWorldUI.Closed += FocusList;
        confirmDialog.Closed += FocusList;
    }

    private void OnDisable()
    {
        actions.System.Pause.performed -= OnBackPerformed;
        actions.System.Disable();
        createWorldUI.Created -= OnWorldCreated;
        createWorldUI.Closed -= FocusList;
        confirmDialog.Closed -= FocusList;
    }

    private void OnDestroy()
    {
        InputBindings.Unregister(actions.asset);
        actions.Dispose();
    }

    private void Start()
    {
        // 에디터에서 배치 확인용으로 넣어 둔 예시 슬롯 제거
        foreach (Transform child in listContent)
        {
            Destroy(child.gameObject);
        }

        Refresh(null);
        fader.FadeIn();
    }

    #region 버튼 OnClick에 연결

    public void Play()
    {
        if (isLeaving || selected == null) return;

        WorldMeta meta = selected.Meta;
        WorldSaveData data = SaveSystem.ReadWorld(meta.id);
        if (data == null)
        {
            confirmDialog.Show($"'{meta.name}'을(를) 불러오지 못했습니다.", "확인", null);
            return;
        }

        WorldSession.Begin(meta, data);
        Leave(SceneFlow.LoadMain);
    }

    public void OpenCreate()
    {
        if (isLeaving) return;
        createWorldUI.Open(slots.Select(slot => slot.Meta.name));
    }

    public void DeleteSelected()
    {
        if (isLeaving || selected == null) return;

        WorldMeta meta = selected.Meta;
        confirmDialog.Show($"'{meta.name}'을(를) 삭제할까요?\n삭제하면 되돌릴 수 없습니다.", "삭제", () =>
        {
            SaveSystem.Delete(meta.id);
            Refresh(null);
        }, "취소");
    }

    public void Back()
    {
        Leave(SceneFlow.LoadTitle);
    }

    #endregion

    // selectId 월드를 다시 선택 (없으면 첫 번째)
    private void Refresh(string selectId)
    {
        foreach (var slot in slots)
        {
            Destroy(slot.gameObject);
        }
        slots.Clear();
        selected = null;

        foreach (WorldMeta meta in SaveSystem.ListWorlds())
        {
            WorldSlotUI slot = Instantiate(slotPrefab, listContent);
            slot.Bind(meta, config);
            slot.Selected += Select;
            slot.Submitted += OnSlotSubmitted;
            slots.Add(slot);
        }

        emptyText.SetActive(slots.Count == 0);
        Select(slots.FirstOrDefault(slot => slot.Meta.id == selectId) ?? slots.FirstOrDefault());
        FocusList();
    }

    // 버튼으로 포커스가 옮겨가도 어떤 월드를 골랐는지 보이도록 강조는 여기서 관리
    private void Select(WorldSlotUI slot)
    {
        if (selected != null) selected.SetHighlighted(false);
        selected = slot;
        if (selected != null) selected.SetHighlighted(true);

        playButton.interactable = selected != null;
        deleteButton.interactable = selected != null;
    }

    private void OnSlotSubmitted(WorldSlotUI slot)
    {
        Select(slot);
        Play();
    }

    // 창을 닫은 뒤 등 키보드 포커스를 목록으로 되돌림 (월드가 없으면 새 월드 버튼)
    private void FocusList()
    {
        if (EventSystem.current == null) return;

        GameObject target = selected != null ? selected.gameObject : newButton.gameObject;
        EventSystem.current.SetSelectedGameObject(target);
    }

    // 맵 생성·저장은 화면이 검게 가려진 동안 처리
    private void OnWorldCreated(string worldName, WorldGenSettings settings)
    {
        if (isLeaving) return;
        isLeaving = true;

        fader.FadeOutThen(() =>
        {
            try
            {
                var (meta, data) = WorldCreator.Create(config, worldName, settings);
                WorldSession.Begin(meta, data);
                SceneFlow.LoadMain();
            }
            catch (Exception e)
            {
                Debug.LogError($"[WorldSelectUI] 월드 생성 실패: {e}");
                isLeaving = false;
                fader.FadeIn();
                createWorldUI.Close();
                confirmDialog.Show("월드를 만들지 못했습니다.", "확인", null);
            }
        });
    }

    private void OnBackPerformed(InputAction.CallbackContext ctx)
    {
        if (isLeaving) return;

        if (confirmDialog.IsOpen)
        {
            confirmDialog.Close();
        }
        else if (createWorldUI.IsOpen)
        {
            // 글자 입력 중의 Esc는 입력 취소로만 사용
            if (!createWorldUI.IsEditingText) createWorldUI.Close();
        }
        else
        {
            Back();
        }
    }

    private void Leave(Action onFaded)
    {
        if (isLeaving) return;
        isLeaving = true;
        fader.FadeOutThen(onFaded);
    }
}
