using UnityEngine;

// 지도·미니맵 공용 설정 (지형 텍스처 해상도, 탐색 안개, 표시 크기)
[CreateAssetMenu(fileName = "MapSetting", menuName = "Common/MapSetting")]
public class MapSetting : ScriptableObject
{
    [Header("지형")]
    [Tooltip("지형 텍스처 한 픽셀이 덮는 거리 (m)")]
    [Min(0.5f)] public float metersPerPixel = 4f;
    [Tooltip("바이옴 경계를 섞는 반폭 (m). 바이옴이 청크 단위라 경계가 사각형으로 보이지 않도록")]
    [Min(0f)] public float biomeBlend = 20f;
    public Color waterColor = new Color(0.25f, 0.45f, 0.75f);
    public Color lavaColor = new Color(0.9f, 0.35f, 0.1f);
    public Color iceColor = new Color(0.72f, 0.86f, 0.95f);
    [Tooltip("지도 뒤 배경 (맵 경계 밖)")]
    public Color outsideColor = new Color(0.08f, 0.08f, 0.1f);

    [Header("탐색 안개")]
    [Tooltip("탐색 기록 칸 크기 (m)")]
    [Min(1f)] public float fogCellSize = 10f;
    [Tooltip("플레이어 주변 이 반경 안이 탐색됨 (m)")]
    [Min(1f)] public float revealRadius = 50f;
    [Tooltip("탐색 갱신 간격 (초)")]
    [Min(0.02f)] public float revealInterval = 0.2f;
    [Tooltip("안 가 본 곳을 덮는 색")]
    public Color fogColor = new Color(0.08f, 0.08f, 0.1f, 1f);

    [Header("표시")]
    [Tooltip("구조물·설치물 점 크기 (UI px)")]
    [Min(1f)] public float dotSize = 6f;
    [Tooltip("플레이어 아이콘 (비우면 임시 도형)")]
    public Sprite playerIcon;
    [Tooltip("스폰 지점 아이콘 (비우면 임시 도형)")]
    public Sprite spawnIcon;
    [Min(1f)] public float iconSize = 16f;

    [Header("미니맵")]
    [Tooltip("미니맵 중심에서 가장자리까지 보이는 거리 (m)")]
    [Min(10f)] public float minimapRange = 150f;

    [Header("전체 지도")]
    [Tooltip("처음 열 때 화면 세로에 보이는 거리 (m)")]
    [Min(10f)] public float worldMapStartSpan = 800f;
    [Tooltip("최대로 확대했을 때 화면 세로에 보이는 거리 (m)")]
    [Min(10f)] public float worldMapMinSpan = 150f;
    [Tooltip("휠 한 칸에 확대·축소하는 배율")]
    [Min(1.01f)] public float worldMapZoomStep = 1.25f;
}
