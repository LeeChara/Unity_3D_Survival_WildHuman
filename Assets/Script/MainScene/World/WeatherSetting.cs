using UnityEngine;

// 날씨 공용 값 (비의 주기·길이·표현)
[CreateAssetMenu(fileName = "WeatherSetting", menuName = "World/WeatherSetting")]
public class WeatherSetting : ScriptableObject
{
    [Header("비")]
    [Tooltip("비가 그친 뒤 다음 비까지의 월드 시간(초) 범위 (하루 = 600초)")]
    public Vector2 rainInterval = new(600f, 1500f);
    [Tooltip("비가 내리는 시간(초) 범위")]
    public Vector2 rainDuration = new(120f, 240f);
    [Tooltip("비가 시작·그칠 때 서서히 바뀌는 시간(초)")]
    [Min(0f)] public float transitionDuration = 10f;
    [Tooltip("비가 오는 동안 젖을 수 있는 대상을 다시 적시는 간격(초)")]
    [Min(0.1f)] public float soakInterval = 1f;

    [Header("표현")]
    [Tooltip("비가 한창일 때 화면 밝기에 곱하는 색 (밤 어둠과 겹침)")]
    public Color rainTint = new(0.72f, 0.76f, 0.86f);
}
