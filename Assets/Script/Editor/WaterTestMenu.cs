using UnityEditor;
using UnityEngine;

// 젖음 규칙 시험용 메뉴 (플레이 모드, 개발용)
// 물 지형·물 양동이가 생기기 전까지 하이어라키에서 고른 대상에 물·화염 속성 공격(피해 0)을 직접 줌
public static class WaterTestMenu
{
    [MenuItem("Tools/물/선택 대상 적시기")]
    private static void Soak() => HitSelection(ElementType.Water);

    [MenuItem("Tools/물/선택 대상에 불 닿게 하기")]
    private static void Fire() => HitSelection(ElementType.Fire);

    [MenuItem("Tools/물/선택 대상 적시기", true)]
    [MenuItem("Tools/물/선택 대상에 불 닿게 하기", true)]
    private static bool CanHit() => Application.isPlaying && Selection.gameObjects.Length > 0;

    private static void HitSelection(ElementType element)
    {
        foreach (GameObject selected in Selection.gameObjects)
        {
            Health health = selected.GetComponentInParent<Health>();
            if (health == null)
            {
                Debug.LogWarning($"[물 시험] 체력이 없는 대상: {selected.name}", selected);
                continue;
            }

            health.TakeHit(new AttackData { damage = 0, element = element });
        }
    }
}
