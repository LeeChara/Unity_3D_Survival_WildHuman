using UnityEngine;

// 음파 공용 규칙
// 음파 = 강타 공격이 금속 물체(Resonant)에 닿으면 퍼지는 부채꼴 (방향은 Resonant의 방향 방식에 따름)
// 음파 자체도 강타라서 다른 금속 물체에 닿으면 다시 음파가 남 (단계마다 약해지고 상한에서 멈춤)
[CreateAssetMenu(fileName = "SoundWaveSetting", menuName = "Common/SoundWaveSetting")]
public class SoundWaveSetting : ScriptableObject
{
    [Header("범위")]
    [Tooltip("세기 1일 때 음파가 닿는 거리 (m, 세기에 비례)")]
    [Min(0.1f)] public float length = 9f;
    [Tooltip("부채꼴 각도 (도)")]
    [Range(1f, 180f)] public float angle = 60f;
    [Tooltip("끝까지 퍼지는 데 걸리는 시간 (초)")]
    [Min(0.01f)] public float spreadTime = 0.3f;
    [Tooltip("반응을 일으키는 대상 레이어 (플레이어, 몬스터, 설치물)")]
    public LayerMask targetLayers;
    [Tooltip("음파를 막는 레이어 (구조물). 파면이 닿으면 그 거리에서 음파 전체가 사라짐 (금속이면 연쇄)")]
    public LayerMask blockLayers;

    [Header("연쇄")]
    [Tooltip("첫 음파 이후 다시 울릴 수 있는 횟수")]
    [Min(0)] public int maxChain = 3;
    [Tooltip("연쇄될 때마다 곱해지는 세기")]
    [Range(0.05f, 1f)] public float chainDecay = 0.6f;
    [Tooltip("이 세기보다 약해지면 더 울리지 않음")]
    [Range(0f, 1f)] public float minPower = 0.2f;

    [Header("반응")]
    [Tooltip("땅속에 숨은 몬스터의 기절 시간 (초)")]
    [Min(0f)] public float burrowedStunDuration = 3f;
    [Tooltip("그 밖의 생물(플레이어 포함)의 기절 시간 (초)")]
    [Min(0f)] public float stunDuration = 0.5f;
    [Tooltip("세기 1일 때 밀려나는 거리 (m, 세기에 비례)")]
    [Min(0f)] public float knockbackDistance = 1.5f;

    [Header("표시")]
    [Tooltip("퍼지는 고리 색")]
    public Color waveColor = new Color(0.85f, 0.95f, 1f, 0.8f);
    [Tooltip("고리 그림 해상도 (반지름 픽셀 수, 작을수록 도트가 굵음)")]
    [Range(8, 64)] public int pixelRadius = 24;
    [Tooltip("고리 두께 (픽셀)")]
    [Range(1, 8)] public int ringThickness = 3;
    [Tooltip("고리 안쪽(지나간 자리)의 진하기")]
    [Range(0f, 1f)] public float fillAlpha = 0.25f;
}
