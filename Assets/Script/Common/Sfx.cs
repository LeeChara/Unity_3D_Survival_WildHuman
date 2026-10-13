using UnityEngine;
using UnityEngine.Audio;

// 위치가 있는 효과음 재생 (SFX 믹서 그룹으로 출력)
// 탑다운 화면이라 3D 감쇠 대신, 카메라가 비추는 지면 지점과의 수평 거리로 볼륨을, 화면 좌우 위치로 팬을 정함
// 재생용 AudioSource는 처음 쓸 때 만들어 씬이 바뀌어도 재사용
public static class Sfx
{
    private const string MixerPath = "Audio/MainMixer";
    private const string GroupName = "SFX";
    private const int PoolSize = 12;
    // 이 거리(수평) 밖의 소리는 들리지 않음 (화면 크기 약 32x26m)
    private const float HearDistance = 20f;
    // 화면 끝의 소리가 치우치는 정도
    private const float MaxPan = 0.6f;

    private static AudioSource[] pool;
    private static int next;

    public static void Play(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
    {
        if (clip == null || volume <= 0f) return;

        float felt = 1f;
        float pan = 0f;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 offset = position - ListenPoint(cam);
            offset.y = 0f;
            felt = 1f - Mathf.Clamp01(offset.magnitude / HearDistance);
            if (felt <= 0f) return;

            Vector3 right = cam.transform.right;
            right.y = 0f;
            pan = Mathf.Clamp(Vector3.Dot(offset, right.normalized) / HearDistance, -1f, 1f) * MaxPan;
        }

        AudioSource source = Take();
        source.clip = clip;
        source.volume = volume * felt;
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
    private static AudioSource Take()
    {
        if (pool == null || pool[0] == null) CreatePool();

        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource candidate = pool[(next + i) % PoolSize];
            if (candidate.isPlaying) continue;
            next = (next + i + 1) % PoolSize;
            return candidate;
        }

        AudioSource oldest = pool[next];
        next = (next + 1) % PoolSize;
        return oldest;
    }

    private static void CreatePool()
    {
        var host = new GameObject("Sfx");
        Object.DontDestroyOnLoad(host);

        AudioMixerGroup group = null;
        var mixer = Resources.Load<AudioMixer>(MixerPath);
        if (mixer != null)
        {
            AudioMixerGroup[] groups = mixer.FindMatchingGroups(GroupName);
            if (groups.Length > 0) group = groups[0];
        }

        pool = new AudioSource[PoolSize];
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
