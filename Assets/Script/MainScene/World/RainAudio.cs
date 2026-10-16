using UnityEngine;

// 비가 오는 동안 빗소리를 반복 재생 (비의 세기만큼 크게)
// 환경음이라 설정의 '환경음' 음량을 곱함 (GameOptions.AmbientVolume, 0이면 재생하지 않음). 출력은 Master 믹서 그룹
[RequireComponent(typeof(AudioSource))]
public class RainAudio : MonoBehaviour
{
    [Tooltip("비가 한창일 때의 음량")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.3f;

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }

    private void Update()
    {
        WeatherSystem weather = WeatherSystem.Instance;
        float strength = weather != null ? weather.RainFactor * GameOptions.AmbientVolume : 0f;

        source.volume = volume * strength;
        if (strength > 0.001f)
        {
            if (!source.isPlaying && source.clip != null) source.Play();
        }
        else if (source.isPlaying)
        {
            source.Stop();
        }
    }
}
