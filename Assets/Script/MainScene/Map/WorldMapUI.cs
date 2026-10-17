using UnityEngine;
using UnityEngine.EventSystems;

// 전체 지도 창 (M키로 열고 닫음). 열려 있어도 게임 시간은 흐름
// 휠: 커서 위치 기준 확대·축소, 드래그: 이동. 열 때마다 플레이어 위치에서 시작
// 이 컴포넌트는 항상 켜져 있는 오브젝트에 두고, 창(panel)만 켜고 끔
public class WorldMapUI : MonoBehaviour, IScrollHandler, IDragHandler
{
    [SerializeField] private GameObject panel;
    [SerializeField] private MapView view;

    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        panel.SetActive(false);
        IsOpen = false;
    }

    private void OnDestroy()
    {
        IsOpen = false;
    }

    public void Toggle()
    {
        SetOpen(!panel.activeSelf);
    }

    public void SetOpen(bool open)
    {
        MapSystem map = MapSystem.Instance;
        if (open && map == null) return;

        panel.SetActive(open);
        IsOpen = open;
        if (!open) return;

        view.Center = map.PlayerPosition;
        view.Zoom = ViewHeight / map.Setting.worldMapStartSpan;
    }

    private float ViewHeight => Mathf.Max(1f, view.Rect.rect.height);

    // 보이는 세로 거리 기준 확대 범위 (가장 넓게는 맵 전체가 들어올 만큼)
    private void ZoomLimits(out float min, out float max)
    {
        MapSystem map = MapSystem.Instance;
        Rect bounds = map.Bounds;
        float maxSpan = Mathf.Max(bounds.width, bounds.height) * 1.5f;
        min = ViewHeight / maxSpan;
        max = ViewHeight / map.Setting.worldMapMinSpan;
    }

    public void OnScroll(PointerEventData eventData)
    {
        float scroll = eventData.scrollDelta.y;
        if (Mathf.Approximately(scroll, 0f)) return;

        ZoomLimits(out float min, out float max);
        float step = MapSystem.Instance.Setting.worldMapZoomStep;
        float oldZoom = view.Zoom;
        float newZoom = Mathf.Clamp(oldZoom * (scroll > 0f ? step : 1f / step), min, max);
        if (Mathf.Approximately(newZoom, oldZoom)) return;

        // 커서 아래 지점이 그대로 있도록 중심을 옮김
        if (view.ScreenToLocal(eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            Vector3 anchor = view.LocalToWorld(local);
            view.Center = anchor + (view.Center - anchor) * (oldZoom / newZoom);
        }
        view.Zoom = newZoom;
        ClampCenter();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!view.ScreenToLocal(eventData.position, eventData.pressEventCamera, out Vector2 now)) return;
        if (!view.ScreenToLocal(eventData.position - eventData.delta, eventData.pressEventCamera, out Vector2 before)) return;

        view.Center += view.LocalToWorld(before) - view.LocalToWorld(now);
        ClampCenter();
    }

    private void ClampCenter()
    {
        Rect bounds = MapSystem.Instance.Bounds;
        Vector3 center = view.Center;
        center.x = Mathf.Clamp(center.x, bounds.xMin, bounds.xMax);
        center.z = Mathf.Clamp(center.z, bounds.yMin, bounds.yMax);
        view.Center = center;
    }
}
