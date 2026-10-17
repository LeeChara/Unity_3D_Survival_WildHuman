using UnityEngine;

// HUD 구석 미니맵: 플레이어를 중심으로 MapSetting.minimapRange만큼 보여 줌
// 전체 지도가 열려 있는 동안은 숨김
public class MinimapUI : MonoBehaviour
{
    [SerializeField] private MapView view;
    [Tooltip("전체 지도가 열리면 숨길 오브젝트 (미니맵 틀 전체)")]
    [SerializeField] private GameObject root;

    private void LateUpdate()
    {
        MapSystem map = MapSystem.Instance;
        if (map == null) return;

        bool visible = !WorldMapUI.IsOpen;
        if (root.activeSelf != visible) root.SetActive(visible);
        if (!visible) return;

        view.Center = map.PlayerPosition;
        // 짧은 변 기준으로 중심~가장자리가 minimapRange
        Vector2 size = view.Rect.rect.size;
        view.Zoom = Mathf.Min(size.x, size.y) * 0.5f / map.Setting.minimapRange;
    }
}
