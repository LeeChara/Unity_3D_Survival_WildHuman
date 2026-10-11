using Unity.Cinemachine;
using UnityEngine;

// 시네머신 카메라에 붙여 화면을 흔듦 (지진 등)
// 카메라 위치 보정(Correction)에만 더하므로 따라가기·댐핑에는 영향을 주지 않음
// 설정의 화면 흔들림 세기(GameOptions.ScreenShake)를 곱해서 적용
public class CameraShake : CinemachineExtension
{
    public static CameraShake Instance { get; private set; }

    [Tooltip("흔들리는 빠르기 (초당 노이즈 진행량)")]
    [SerializeField, Min(0f)] private float frequency = 15f;

    private float amplitude;
    private float duration;
    private float remaining;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
    }

    // 이미 흔들리는 중이면 더 센 쪽을 따르고, 남은 시간은 더 긴 쪽으로
    // 세기는 시작할 때 가장 크고 끝날수록 줄어듦
    public void Shake(float amplitude, float duration)
    {
        if (amplitude <= 0f || duration <= 0f) return;

        float current = CurrentAmplitude();
        if (amplitude >= current)
        {
            this.amplitude = amplitude;
            this.duration = duration;
            remaining = duration;
        }
        else
        {
            remaining = Mathf.Max(remaining, duration);
            this.duration = Mathf.Max(this.duration, remaining);
        }
    }

    private void Update()
    {
        if (remaining > 0f) remaining -= Time.deltaTime;
    }

    private float CurrentAmplitude()
    {
        if (remaining <= 0f || duration <= 0f) return 0f;
        return amplitude * (remaining / duration);
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Noise) return;

        float strength = CurrentAmplitude() * GameOptions.ScreenShake;
        if (strength <= 0f) return;

        // 화면 기준 가로·세로로만 흔듦 (Perlin 노이즈는 0~1이라 -1~1로 옮김)
        float t = Time.time * frequency;
        float x = (Mathf.PerlinNoise(t, 0.3f) * 2f - 1f) * strength;
        float y = (Mathf.PerlinNoise(0.7f, t) * 2f - 1f) * strength;
        state.PositionCorrection += state.RawOrientation * new Vector3(x, y, 0f);
    }
}
