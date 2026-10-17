using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 지도 한 장의 화면 표시 (미니맵·전체 지도 공용)
// 이 오브젝트(Mask가 붙은 사각 영역) 아래에 표시용 자식을 코드로 만듦
// - 회전판: 카메라 Y 회전만큼 돌려서 화면 위쪽 = 카메라가 보는 방향
//   - 지형판: 1 단위 = 1m, Zoom만큼 확대 (지형·안개 텍스처)
//   - 점: 확대하지 않고 위치만 옮김 (구조물·설치물 점, 스폰 지점, 플레이어)
[RequireComponent(typeof(RectTransform))]
public class MapView : MonoBehaviour
{
    [Tooltip("이 거리(m) 밖의 점은 계산하지 않음 (0이면 보이는 영역 전체 기준)")]
    [SerializeField, Min(0f)] private float markerCullRange;

    // 화면 중심에 올 월드 위치 (y 무시)
    public Vector3 Center { get; set; }
    // UI 단위 / m
    public float Zoom { get; set; } = 1f;
    public RectTransform Rect { get; private set; }

    private MapSystem map;
    private RectTransform rotator;
    private RectTransform content;
    private RectTransform markerLayer;
    private Image spawnIcon;
    private Image playerIcon;
    private readonly List<Image> dots = new();
    private readonly List<MapSystem.Marker> markers = new();
    private float yaw;

    private void Awake()
    {
        Rect = (RectTransform)transform;
    }

    private void Start()
    {
        map = MapSystem.Instance;
        if (map == null)
        {
            enabled = false;
            return;
        }
        map.EnsureInitialized();
        Build();
    }

    private void Build()
    {
        rotator = CreateRect("Rotator", Rect);
        content = CreateRect("Content", rotator);

        // 지형·안개는 경계 영역을 1m = 1단위로 덮음
        Rect bounds = map.Bounds;
        RawImage terrain = CreateRect("Terrain", content).gameObject.AddComponent<RawImage>();
        terrain.texture = map.TerrainTexture;
        Place(terrain.rectTransform, bounds);
        RawImage fog = CreateRect("Fog", content).gameObject.AddComponent<RawImage>();
        fog.texture = map.FogTexture;
        fog.color = map.Setting.fogColor;
        Place(fog.rectTransform, bounds);
        terrain.raycastTarget = false;
        fog.raycastTarget = false;

        markerLayer = CreateRect("Markers", rotator);

        MapSetting setting = map.Setting;
        spawnIcon = CreateImage("Spawn", rotator, setting.spawnIcon != null ? setting.spawnIcon : MapSprites.Diamond, setting.iconSize);
        playerIcon = CreateImage("Player", rotator, setting.playerIcon != null ? setting.playerIcon : MapSprites.PlayerMark, setting.iconSize);
        if (setting.spawnIcon == null) spawnIcon.color = new Color(1f, 0.95f, 0.6f);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, float size)
    {
        Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.rectTransform.sizeDelta = new Vector2(size, size);
        return image;
    }

    private static void Place(RectTransform rect, Rect bounds)
    {
        rect.anchoredPosition = bounds.center;
        rect.sizeDelta = bounds.size;
    }

    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera != null) yaw = camera.transform.eulerAngles.y;

        // 월드 방향 (sin yaw, cos yaw)가 지도 위쪽을 향하도록 반시계로 yaw만큼 돌림
        rotator.localRotation = Quaternion.Euler(0f, 0f, yaw);
        content.localScale = new Vector3(Zoom, Zoom, 1f);
        content.anchoredPosition = -ToPlane(Center) * Zoom;

        Quaternion upright = Quaternion.Euler(0f, 0f, -yaw);
        PlaceIcon(spawnIcon.rectTransform, map.SpawnPoint, upright);
        PlaceIcon(playerIcon.rectTransform, map.PlayerPosition, upright);

        UpdateDots();
    }

    private void PlaceIcon(RectTransform icon, Vector3 world, Quaternion upright)
    {
        icon.anchoredPosition = (ToPlane(world) - ToPlane(Center)) * Zoom;
        icon.localRotation = upright;
    }

    private void UpdateDots()
    {
        map.GetMarkers(markers);

        // 보이는 영역의 대각선 절반 (회전해도 이 원 밖이면 화면 밖)
        float range = markerCullRange > 0f ? markerCullRange : Rect.rect.size.magnitude * 0.5f / Zoom;
        float rangeSq = range * range;
        Vector2 center = ToPlane(Center);
        float size = map.Setting.dotSize;

        int used = 0;
        foreach (MapSystem.Marker marker in markers)
        {
            Vector2 offset = ToPlane(marker.position) - center;
            if (offset.sqrMagnitude > rangeSq) continue;

            if (used == dots.Count) dots.Add(CreateImage("Dot", markerLayer, MapSprites.Circle, size));
            Image dot = dots[used++];
            dot.gameObject.SetActive(true);
            dot.color = marker.color;
            dot.rectTransform.sizeDelta = new Vector2(size, size);
            dot.rectTransform.anchoredPosition = offset * Zoom;
        }
        for (int i = used; i < dots.Count; i++)
        {
            if (dots[i].gameObject.activeSelf) dots[i].gameObject.SetActive(false);
        }
    }

    private static Vector2 ToPlane(Vector3 world) => new(world.x, world.z);

    // 이 영역 안의 로컬 위치(UI 단위, 중심 기준) → 월드 위치
    public Vector3 LocalToWorld(Vector2 local)
    {
        Vector2 plane = (Vector2)(Quaternion.Euler(0f, 0f, -yaw) * local) / Zoom + ToPlane(Center);
        return new Vector3(plane.x, 0f, plane.y);
    }

    // 화면 위치 → 이 영역의 로컬 위치 (중심 기준)
    public bool ScreenToLocal(Vector2 screen, Camera eventCamera, out Vector2 local)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, eventCamera, out local)) return false;
        local -= Rect.rect.center;
        return true;
    }
}
