using UnityEngine;

// 월드에 자연히 생기는 구조물 한 종류 (유적, 학습용 구조물 등)
// 배치는 prefab 자체가 담당 (부품을 자식으로 두고 프리팹 편집기에서 디자인)
// 월드 생성 때 StructureStep이 규칙에 따라 위치를 정하고, 반경 안의 자원은 지움
[CreateAssetMenu(fileName = "WorldStructure", menuName = "World/WorldStructureData")]
public class WorldStructureData : ScriptableObject
{
    [Tooltip("저장용 고유 id (한 번 정하면 바꾸지 않음)")]
    public string id;
    [Tooltip("루트에 WorldStructure가 붙은 프리팹")]
    public WorldStructure prefab;
    [Tooltip("생성할지 여부 (끄면 새 월드에 생기지 않음, 이미 생긴 것은 그대로)")]
    public bool enabled = true;

    [Header("자리")]
    [Tooltip("점유 반경 (m). 이 안의 자원은 지워지고, 이후에도 자원·설치물이 생기지 않음")]
    [Min(0.5f)] public float radius = 8f;
    [Tooltip("생길 수 있는 바이옴 (비우면 바이옴 무관)")]
    public BiomeData[] biomes;
    [Tooltip("회전을 90도 단위로 무작위로 정함 (끄면 항상 0도)")]
    public bool randomRotation = true;

    [Header("지도")]
    [Tooltip("지도·미니맵에 찍을 점 색 (알파 0이면 표시하지 않음, 탐색한 곳에서만 보임)")]
    public Color mapColor = new Color(0.85f, 0.75f, 0.35f);

    [Header("개수·분포")]
    [Tooltip("자리를 먼저 차지하는 순서 (높을수록 먼저). 꼭 있어야 하는 구조물을 높게")]
    public int priority;
    [Tooltip("작은 맵(16×16 청크) 기준 개수 범위. 맵이 크면 범위가 넓이에 비례해서 늘어난 뒤 그 안에서 고름")]
    public Vector2Int count = new(1, 2);
    [Tooltip("켜면 맵이 커질수록 개수 범위를 넓이에 비례해서 늘림 (끄면 맵 크기와 관계없이 count 그대로)")]
    public bool scaleCountWithMap = true;
    [Tooltip("시작 지점에서의 거리 범위 (m). y가 0이면 상한 없음")]
    public Vector2 distanceBand = new(0f, 0f);
    [Tooltip("같은 종류끼리 최소 거리 (중심 기준, m)")]
    [Min(0f)] public float sameTypeSpacing = 400f;

    [Header("기준점")]
    [Tooltip("BiomeStep 기준점 id (비우면 사용 안 함). 정하면 기준점에 가장 가까운 자리부터 차지하고, anchorRadius 밖에는 놓지 않음")]
    public string anchor;
    [Tooltip("기준점에서 벗어날 수 있는 최대 거리 (m)")]
    [Min(0f)] public float anchorRadius = 400f;

    [Header("발견 보장")]
    [Tooltip("시작 지점 근처 거리 띠(nearBand) 안에 최소 하나를 먼저 놓음")]
    public bool guaranteeNear;
    [Tooltip("보장용 거리 띠 (시작 지점 기준, m)")]
    public Vector2 nearBand = new(150f, 350f);
}
