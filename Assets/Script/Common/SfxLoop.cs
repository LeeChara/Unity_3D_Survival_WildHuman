using UnityEngine;

// 이 오브젝트 위치에서 반복 재생되는 효과음 (불타는 소리, 모닥불, 도화선, 재련기 가동 등)
// Sfx와 같은 방식으로 화면 중심과의 거리로 볼륨·팬을 매 프레임 다시 정하고, 켜고 끌 때는 짧게 페이드
// 프리팹에 붙여 playOnEnable로 계속 틀거나, 코드에서 Ensure로 붙여 Play/Stop으로 켜고 끔
// 하나의 오브젝트에 여러 소리가 필요하면 코드에서 각각 다른 SfxLoop를 붙임 (Ensure는 같은 cue를 쓰는 것을 찾음)
public class SfxLoop : MonoBehaviour
{
    [SerializeField] private SfxCue cue;
    [Tooltip("켜질 때 바로 재생 (모닥불·용암처럼 늘 소리가 나는 것)")]
    [SerializeField] private bool playOnEnable;
    [Tooltip("켜고 끌 때의 페이드 시간 (초)")]
    [SerializeField, Min(0.01f)] private float fadeTime = 0.25f;

    private AudioSource source;
    private bool wanted;
    private float fade;
    private float volumeScale = 1f;

    public SfxCue Cue => cue;
    public bool IsPlaying => wanted;

    // 같은 cue를 쓰는 SfxLoop를 찾고, 없으면 붙임
    public static SfxLoop Ensure(GameObject target, SfxCue cue)
    {
        foreach (SfxLoop existing in target.GetComponents<SfxLoop>())
        {
            if (existing.cue == cue) return existing;
        }
        SfxLoop loop = target.AddComponent<SfxLoop>();
        loop.cue = cue;
        return loop;
    }

    private void OnEnable()
    {
        fade = 0f;
        if (playOnEnable) Play();
    }

    private void OnDisable()
    {
        wanted = false;
        fade = 0f;
        if (source != null) source.Stop();
    }

    public void Play(float volume = 1f)
    {
        volumeScale = volume;
        if (cue == null || cue.FirstClip == null) return;
        wanted = true;
        enabled = true;
    }

    public void SetVolume(float volume) => volumeScale = volume;

    public void Stop()
    {
        wanted = false;
    }

    private void Update()
    {
        float step = Time.unscaledDeltaTime / fadeTime;
        fade = Mathf.MoveTowards(fade, wanted ? 1f : 0f, step);

        if (fade <= 0f)
        {
            if (source != null && source.isPlaying) source.Stop();
            return;
        }

        // 일시정지 중에는 멈춤 (UI 소리가 아니므로)
        bool paused = Time.timeScale <= 0f;
        bool audible = Sfx.Evaluate(transform.position, out float felt, out float pan);
        if (!audible || paused)
        {
            if (source != null && source.isPlaying) source.Pause();
            return;
        }

        if (source == null) CreateSource();
        source.volume = cue.volume * volumeScale * felt * fade;
        source.panStereo = pan;
        // 멈춰 두었던 것은 이어서 재생
        if (!source.isPlaying) source.UnPause();
        if (!source.isPlaying)
        {
            if (source.clip != cue.FirstClip)
            {
                source.clip = cue.FirstClip;
                // 여러 개가 동시에 켜져도 같은 박자로 겹치지 않도록 시작 지점을 흩어 둠
                source.time = Random.Range(0f, source.clip.length);
                source.pitch = Random.Range(cue.pitchRange.x, cue.pitchRange.y);
            }
            source.Play();
        }
    }

    private void CreateSource()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = Sfx.Group;
    }
}
