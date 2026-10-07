using UnityEngine;

// 채집 가능한 자원(Prop) 한 종류의 수치 (자원 프리팹의 PropHealth가 참조)
[CreateAssetMenu(fileName = "PropData", menuName = "Prop/PropData")]
public class PropData : ScriptableObject
{
    [Tooltip("저장·로드 시 식별용 고유 ID (한 번 정하면 변경하지 않음)")]
    public string id;
    [Tooltip("마우스를 올렸을 때 표시할 이름 (비워두면 표시하지 않음)")]
    public string displayName;
    [Min(1)] public float maxHealth = 30f;
    public ToolWeakness toolWeakness = new ToolWeakness { tool = ToolType.None, multiplier = 5f };
    [Tooltip("파괴될 때 떨어뜨릴 아이템")]
    public DropTable dropTable;
    [Tooltip("파괴될 때 생성할 이펙트 (비워두면 생성하지 않음)")]
    public GameObject destroyEffectPrefab;
}
