using UnityEngine;

// 피격 반짝임 공용 설정
// 같은 에셋을 참조하는 대상(몬스터, 플레이어 등)은 값을 일괄 적용받음
[CreateAssetMenu(fileName = "HitFlashSetting", menuName = "Common/HitFlashSetting")]
public class HitFlashSetting : ScriptableObject
{
    [Tooltip("파츠 원래 색에 곱해지는 색")]
    public Color color = new Color(1f, 0.3f, 0.3f);
    public float duration = 0.1f;
}
