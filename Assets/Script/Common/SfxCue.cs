using UnityEngine;

// 효과음 하나 (재생할 때마다 클립 중 하나를 고르고 피치를 조금씩 흔들어 반복이 덜 거슬리게 함)
// 같은 소리가 한꺼번에 여러 번 나면(연속 타격, 여러 아이템 줍기 등) minInterval 안의 재생은 건너뜀
[CreateAssetMenu(fileName = "SfxCue", menuName = "Audio/SfxCue")]
public class SfxCue : ScriptableObject
{
    [Tooltip("효과음 관리 창에 표시할 설명 (게임에서는 쓰지 않음)")]
    public string description;
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("재생할 때마다 이 범위에서 피치를 고름")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [Tooltip("이 시간(초) 안에 다시 재생하면 건너뜀 (0이면 제한 없음)")]
    [Min(0f)] public float minInterval = 0.03f;
    [Tooltip("이 효과음이 동시에 날 수 있는 최대 개수 (넘으면 건너뜀, 한 소리가 다른 소리를 모두 밀어내지 않도록)")]
    [Min(1)] public int maxVoices = 4;

    // 에셋은 플레이 사이에 유지되므로 저장하지 않는 마지막 재생 시각 (실제 시간 기준, 일시정지 중 UI 소리도 막지 않도록)
    [System.NonSerialized] private float lastPlayed = -999f;

    public bool IsEmpty => clips == null || clips.Length == 0;

    // 재생할 클립과 피치를 고름. 간격 제한에 걸리거나 클립이 없으면 false
    public bool TryPick(out AudioClip clip, out float pitch)
    {
        clip = null;
        pitch = 1f;
        if (IsEmpty) return false;

        float now = Time.realtimeSinceStartup;
        if (now - lastPlayed < minInterval && lastPlayed <= now) return false;
        lastPlayed = now;

        clip = clips[Random.Range(0, clips.Length)];
        pitch = Random.Range(pitchRange.x, pitchRange.y);
        return clip != null;
    }

    // 반복 재생용 (클립 하나만 씀)
    public AudioClip FirstClip => IsEmpty ? null : clips[0];

    // 비어 있으면 대체 소리 (직렬화 필드는 ??로 비교하면 Unity의 null 판정을 거치지 않으므로 이걸 씀)
    public static SfxCue Or(SfxCue cue, SfxCue fallback) => cue != null ? cue : fallback;
}
