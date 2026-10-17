using UnityEngine;
using UnityEngine.Audio;

// 위치가 있는 효과음 재생 (SFX 믹서 그룹으로 출력)
// 탑다운 화면이라 3D 감쇠 대신, 카메라가 비추는 지면 지점과의 수평 거리로 볼륨을, 화면 좌우 위치로 팬을 정함
// 재생용 AudioSource는 처음 쓸 때 만들어 씬이 바뀌어도 재사용
public static class Sfx
{
    private const string MixerPath = "Audio/MainMixer";
    private const string GroupName = "SFX";
    private const int PoolSize = 16;
    // 이 거리(수평) 밖의 소리는 들리지 않음 (화면 크기 약 32x26m)
    private const float HearDistance = 20f;
    // 화면 끝의 소리가 치우치는 정도
    private const float MaxPan = 0.6f;

    private static AudioSource[] pool;
    // 소스별로 지금 재생 중인 효과음 (같은 효과음의 동시 재생 수를 셀 때 사용, 클립만 재생하면 null)
    private static SfxCue[] owners;
    private static int next;
    private static AudioMixerGroup group;

    public static AudioMixerGroup Group
    {
        get
        {
            if (pool == null || pool[0] == null) CreatePool();
            return group;
        }
    }

    public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
    {
        if (clip == null || volume <= 0f) return;
        if (!Evaluate(position, out float felt, out float pan)) return;

        PlayRaw(clip, volume * felt, pitch, pan, null);
    }

    // 효과음 에셋 재생 (클립·피치는 에셋이 고름, volumeScale·pitchScale은 상황별 보정)
    public static void Play(SfxCue cue, Vector3 position, float volumeScale = 1f, float pitchScale = 1f)
    {
        if (cue == null || volumeScale <= 0f) return;
        if (!Evaluate(position, out float felt, out float pan)) return;
        if (IsSaturated(cue)) return;
        if (!cue.TryPick(out AudioClip clip, out float pitch)) return;

        PlayRaw(clip, cue.volume * volumeScale * felt, pitch * pitchScale, pan, cue);
    }

    // 위치 없는 소리 (UI, 플레이어 자신의 상태 알림 등)
    public static void PlayUI(SfxCue cue, float volumeScale = 1f)
    {
        if (cue == null || volumeScale <= 0f) return;
        if (IsSaturated(cue)) return;
        if (!cue.TryPick(out AudioClip clip, out float pitch)) return;

        PlayRaw(clip, cue.volume * volumeScale, pitch, 0f, cue);
    }

    // 같은 효과음이 이미 maxVoices개 재생 중인지
    private static bool IsSaturated(SfxCue cue)
    {
        if (pool == null || pool[0] == null) return false;

        int playing = 0;
        for (int i = 0; i < PoolSize; i++)
        {
            if (owners[i] == cue && pool[i].isPlaying && ++playing >= cue.maxVoices) return true;
        }
        return false;
    }

    // 위치의 소리가 들리는 정도(0~1)와 좌우 팬. 들리지 않을 만큼 멀면 false
    public static bool Evaluate(Vector3 position, out float felt, out float pan)
    {
        felt = 1f;
        pan = 0f;
        Camera cam = Camera.main;
        if (cam == null) return true;

        Vector3 offset = position - ListenPoint(cam);
        offset.y = 0f;
        felt = 1f - Mathf.Clamp01(offset.magnitude / HearDistance);
        if (felt <= 0f) return false;

        Vector3 right = cam.transform.right;
        right.y = 0f;
        pan = Mathf.Clamp(Vector3.Dot(offset, right.normalized) / HearDistance, -1f, 1f) * MaxPan;
        return true;
    }

    private static void PlayRaw(AudioClip clip, float volume, float pitch, float pan, SfxCue owner)
    {
        AudioSource source = Take(out int index);
        owners[index] = owner;
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.panStereo = pan;
        source.Play();
    }

    // 카메라 중심 시선이 지면(높이 0)에 닿는 점 (보통 플레이어 위치)
    private static Vector3 ListenPoint(Camera cam)
    {
        Transform t = cam.transform;
        Vector3 dir = t.forward;
        if (dir.y > -0.01f) return t.position;
        return t.position + dir * (-t.position.y / dir.y);
    }

    // 쉬고 있는 소스를 우선 쓰고, 모두 재생 중이면 가장 오래된 것부터 끊음
    private static AudioSource Take(out int index)
    {
        if (pool == null || pool[0] == null) CreatePool();

        for (int i = 0; i < PoolSize; i++)
        {
            index = (next + i) % PoolSize;
            if (pool[index].isPlaying) continue;
            next = (index + 1) % PoolSize;
            return pool[index];
        }

        index = next;
        next = (next + 1) % PoolSize;
        return pool[index];
    }

    private static void CreatePool()
    {
        var host = new GameObject("Sfx");
        Object.DontDestroyOnLoad(host);

        group = null;
        var mixer = Resources.Load<AudioMixer>(MixerPath);
        if (mixer != null)
        {
            AudioMixerGroup[] groups = mixer.FindMatchingGroups(GroupName);
            if (groups.Length > 0) group = groups[0];
        }

        pool = new AudioSource[PoolSize];
        owners = new SfxCue[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = group;
            pool[i] = source;
        }
        next = 0;
    }
}
