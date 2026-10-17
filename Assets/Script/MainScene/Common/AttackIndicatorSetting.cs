using UnityEngine;

// 공격 범위 표시의 공용 값 (색·도트 크기 등)
[CreateAssetMenu(fileName = "AttackIndicatorSetting", menuName = "Setting/AttackIndicatorSetting")]
public class AttackIndicatorSetting : ScriptableObject
{
    [Tooltip("WildHuman/AttackIndicator 셰이더를 쓰는 머티리얼")]
    public Material material;

    [Header("색")]
    public Color areaColor = new Color(1f, 0.25f, 0.15f, 0.25f);
    public Color fillColor = new Color(1f, 0.3f, 0.15f, 0.45f);
    public Color edgeColor = new Color(1f, 0.35f, 0.2f, 0.9f);

    [Header("모양")]
    [Tooltip("도트 한 칸 크기 (m)")]
    [Min(0.01f)] public float pixelSize = 0.1f;
    [Tooltip("바닥에서 띄우는 높이 (m)")]
    [Min(0f)] public float lift = 0.03f;

    [Header("사라짐")]
    [Tooltip("공격이 나간 뒤 사라지는 데 걸리는 시간 (초)")]
    [Min(0.01f)] public float fadeDuration = 0.2f;
}
