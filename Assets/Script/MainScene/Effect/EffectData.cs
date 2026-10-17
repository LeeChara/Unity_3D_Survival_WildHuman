using System;
using UnityEngine;

// 스프라이트 파티클 이펙트 한 종류 (Effects.Play로 재생)
// 이미터마다 같은 규칙의 파티클을 한 번에 뿌림 (예: 폭발 = 파편 + 연기 + 불씨)
[CreateAssetMenu(fileName = "EffectData", menuName = "Effect/EffectData")]
public class EffectData : ScriptableObject
{
    [Tooltip("이펙트 관리 창에 표시할 설명")]
    public string description;
    public ParticleEmitter[] emitters;
}

[Serializable]
public class ParticleEmitter
{
    [Tooltip("파티클 그림. 여러 장이면 수명 동안 순서대로 한 번 재생 (loopFrames면 frameRate로 반복)")]
    public Sprite[] frames;
    [Tooltip("파티클마다 그림 한 장을 무작위로 골라 끝까지 씀 (예: 모양이 다른 파편)")]
    public bool randomFrame;
    public bool loopFrames;
    [Min(0.1f)] public float frameRate = 10f;
    // 임시 그림 기록 (이펙트 관리 창에서 교체한 그림을 되돌릴 때 사용, 게임에서는 쓰지 않음)
    [HideInInspector] public Sprite[] placeholderFrames;

    [Header("색")]
    [Tooltip("두 색 사이에서 무작위")]
    public Color colorA = Color.white;
    public Color colorB = Color.white;
    [Tooltip("재생할 때 준 색을 곱할지 (예: 광석 파편은 광석 색, 불티는 그대로)")]
    public bool useTint = true;

    [Header("수")]
    public Vector2Int count = new Vector2Int(3, 5);
    [Tooltip("파티클마다 나타나기 전 대기 시간 범위 (초)")]
    public Vector2 delay = Vector2.zero;

    [Header("위치 (대상 그림 범위 기준, 점에서 재생하면 minArea만 사용)")]
    [Tooltip("범위 대비 중심 (x: 0이 가운데, y: 0이 바닥 1이 꼭대기)")]
    public Vector2 center = new Vector2(0f, 0.5f);
    [Tooltip("범위 너비·높이 대비 퍼지는 크기")]
    public Vector2 area = new Vector2(0.6f, 0.4f);
    [Tooltip("최소 퍼지는 크기 (m)")]
    public Vector2 minArea = new Vector2(0.2f, 0.2f);

    [Header("움직임")]
    [Tooltip("발사 방향 (도, 0이 화면 오른쪽, 90이 위)")]
    public float angle = 90f;
    [Tooltip("발사 방향에서 벌어지는 각도 (전체 폭)")]
    [Range(0f, 360f)] public float spread = 120f;
    public Vector2 speed = new Vector2(1.5f, 3f);
    [Tooltip("아래로 당기는 가속 (음수면 위로 떠오름)")]
    public float gravity = 9f;
    [Tooltip("속도 감쇠 (초당 비율)")]
    [Min(0f)] public float drag = 0f;
    [Tooltip("좌우로 흔들리는 폭 (m, 예: 떨어지는 잎사귀)")]
    [Min(0f)] public float wobble = 0f;
    [Tooltip("바닥(대상의 발밑 높이)에 닿으면 멈춤")]
    public bool stopOnGround = true;

    [Header("수명·크기")]
    public Vector2 lifetime = new Vector2(0.4f, 0.7f);
    [Tooltip("크기 배율 범위 (1이면 스프라이트 원래 크기)")]
    public Vector2 size = new Vector2(1f, 1f);
    [Tooltip("수명 비율(0~1)에 따른 크기 배율")]
    public AnimationCurve sizeOverLife = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [Tooltip("이 수명 비율부터 계단식으로 흐려짐 (1이면 끝에 바로 사라짐)")]
    [Range(0f, 1f)] public float fadeStart = 0.6f;
}
