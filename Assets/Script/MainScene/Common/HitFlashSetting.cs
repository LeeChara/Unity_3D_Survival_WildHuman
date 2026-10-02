using UnityEngine;

// 파츠 반짝임 공용 설정 (피격, 음식 회복)
// 같은 에셋을 참조하는 대상(몬스터, 플레이어, 자원)은 값을 일괄 적용받음
[CreateAssetMenu(fileName = "HitFlashSetting", menuName = "Common/HitFlashSetting")]
public class HitFlashSetting : ScriptableObject
{
    [Header("피격")]
    [Tooltip("캐릭터(몬스터, 플레이어) 피격 시 파츠 원래 색에 곱해지는 색")]
    public Color color = new Color(1f, 0.3f, 0.3f);
    [Tooltip("자원(Prop) 피격 시 곱해지는 색")]
    public Color resourceColor = new Color(0.5f, 0.5f, 0.5f);
    public float duration = 0.1f;

    [Header("음식 회복")]
    [Tooltip("음식을 먹었을 때 파츠 원래 색에 곱해지는 색")]
    public Color healColor = new Color(0.6f, 1f, 0.5f);
    public float healDuration = 0.25f;
}
