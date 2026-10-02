using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 핫바에서 설치 아이템을 고르면 커서 위치에 설치물 모습을 반투명하게 표시
// 설치 가능하면 초록, 불가능하면 빨강 (판정·회전은 실제 설치와 같은 PlacementManager 사용)
// 설치물 안의 스프라이트(울타리 기둥 여러 개 등)를 모두 같은 상대 위치에 복제해서 보여줌
public class PlayerPlacementPreview : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerHotbar hotbar;

    [SerializeField] private Color validColor = new Color(0.5f, 1f, 0.5f, 0.6f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.4f, 0.4f, 0.6f);

    private Vector2 pointerScreenPos;

    // 미리보기 스프라이트들의 부모 (켜고 끄기용)
    private GameObject preview;
    // 프리팹의 스프라이트 하나당 미리보기 스프라이트 하나 (부족하면 늘리고 남으면 숨김)
    private readonly List<SpriteRenderer> ghosts = new();
    // 각 스프라이트의 프리팹 루트 기준 위치 (설치 회전을 적용해 배치)
    private readonly List<Vector3> ghostOffsets = new();
    private int ghostCount;

    // 현재 미리보기 중인 아이템 (설치 아이템이 아니면 null)
    private PlaceableItemData current;
    private bool started;

    private void Awake()
    {
        preview = new GameObject("PlacementPreview");
        preview.SetActive(false);
    }

    private void OnEnable()
    {
        hotbar.SelectionChanged += OnSelectionChanged;
        inventory.SlotChanged += OnSlotChanged;

        if (started) Refresh();
    }

    // 인벤토리 칸이 Awake에서 만들어지므로 첫 갱신은 Start에서
    private void Start()
    {
        started = true;
        Refresh();
    }

    private void OnDisable()
    {
        hotbar.SelectionChanged -= OnSelectionChanged;
        inventory.SlotChanged -= OnSlotChanged;

        // 다시 켜질 때 같은 아이템이어도 미리보기가 다시 표시되도록 초기화
        current = null;
        Hide();
    }

    private void OnDestroy()
    {
        if (preview != null) Destroy(preview);
    }

    void OnPoint(InputValue value)
    {
        pointerScreenPos = value.Get<Vector2>();
    }

    private void Update()
    {
        PlacementManager manager = PlacementManager.Instance;
        if (current == null || manager == null) return;

        if (!PointerUtil.TryGetGroundPoint(pointerScreenPos, PointerUtil.GroundHeight, out Vector3 position))
        {
            SetGhostsVisible(false);
            return;
        }

        Quaternion rotation = manager.GetRotation(current, transform.position, position);
        Color color = manager.CanPlace(current, transform.position, position) ? validColor : invalidColor;

        for (int i = 0; i < ghostCount; i++)
        {
            SpriteRenderer ghost = ghosts[i];
            ghost.transform.SetPositionAndRotation(position + rotation * ghostOffsets[i], manager.BillboardRotation);
            ghost.color = color;
        }
        SetGhostsVisible(true);
    }

    private void OnSelectionChanged(int index)
    {
        Refresh();
    }

    // 선택 칸의 아이템이 바뀌었을 때만 갱신 (설치해서 다 쓴 경우 등)
    private void OnSlotChanged(int index)
    {
        if (index == hotbar.SelectedIndex) Refresh();
    }

    private void Refresh()
    {
        ItemStack stack = hotbar.SelectedStack;
        PlaceableItemData item = stack.IsEmpty ? null : stack.item as PlaceableItemData;
        if (item == current) return;

        current = item;
        if (current == null || current.placedPrefab == null)
        {
            current = null;
            Hide();
            return;
        }

        ApplyLook(current.placedPrefab);

        // 위치가 정해지기 전 한 프레임 동안 이전 자리에 보이지 않도록 Update에서 켬
        SetGhostsVisible(false);
        preview.SetActive(true);
    }

    // 설치물 프리팹의 스프라이트마다 모습(스프라이트·좌우 반전·크기·머티리얼)과 루트 기준 위치를 복사
    private void ApplyLook(GameObject prefab)
    {
        Transform root = prefab.transform;
        SpriteRenderer[] sources = prefab.GetComponentsInChildren<SpriteRenderer>(true);

        ghostCount = 0;
        ghostOffsets.Clear();
        foreach (SpriteRenderer source in sources)
        {
            if (source.sprite == null) continue;

            SpriteRenderer ghost = GetGhost(ghostCount);
            ghost.sprite = source.sprite;
            ghost.flipX = source.flipX;
            ghost.sharedMaterial = source.sharedMaterial;
            ghost.transform.localScale = source.transform.lossyScale;
            ghostOffsets.Add(root.InverseTransformPoint(source.transform.position));
            ghostCount++;
        }

        for (int i = ghostCount; i < ghosts.Count; i++)
        {
            ghosts[i].gameObject.SetActive(false);
        }
    }

    // 미리보기 스프라이트마다 앞뒤 정렬되도록 DepthSorter(SortingGroup 포함)를 붙임
    private SpriteRenderer GetGhost(int index)
    {
        if (index < ghosts.Count)
        {
            ghosts[index].gameObject.SetActive(true);
            return ghosts[index];
        }

        GameObject ghostObject = new GameObject($"Ghost{index}");
        ghostObject.transform.SetParent(preview.transform, false);
        SpriteRenderer ghost = ghostObject.AddComponent<SpriteRenderer>();
        ghostObject.AddComponent<DepthSorter>();
        ghosts.Add(ghost);
        return ghost;
    }

    private void SetGhostsVisible(bool visible)
    {
        for (int i = 0; i < ghostCount; i++)
        {
            ghosts[i].enabled = visible;
        }
    }

    private void Hide()
    {
        if (preview != null) preview.SetActive(false);
    }
}
