using UnityEngine;

// 채집 가능한 자원(Prop) 한 종류의 수치 (자원 프리팹의 PropHealth가 참조)
[CreateAssetMenu(fileName = "PropData", menuName = "Prop/PropData")]
public class PropData : ScriptableObject
{
    [Tooltip("저장·로드 시 식별용 고유 ID (한 번 정하면 변경하지 않음)")]
    public string id;
    [Tooltip("마우스를 올렸을 때 표시할 이름 (비워두면 표시하지 않음)")]
    public string displayName;
    [Tooltip("도감에 표시할 설명 (특징, 상호작용 등)")]
    [TextArea] public string description;
    [Min(1)] public float maxHealth = 30f;
    public ToolWeakness toolWeakness = new ToolWeakness { tool = ToolType.None, multiplier = 5f };
    [Tooltip("채집에 필요한 도구 티어 (0이면 제한 없음, 1 이상이면 toolWeakness의 도구로 이 티어 이상이어야 피해를 받음)")]
    [Min(0)] public int requiredTier = 0;
    [Tooltip("파괴될 때 떨어뜨릴 아이템")]
    public DropTable dropTable;
    [Tooltip("파괴될 때 생성할 이펙트 (비워두면 생성하지 않음)")]
    public GameObject destroyEffectPrefab;

    [Header("불")]
    [Tooltip("화염 속성 공격에 맞으면 불이 붙는지 (프리팹에 PropBurner가 있어야 함)")]
    public bool flammable;
    [Tooltip("불이 붙은 뒤 다 타서 사라지기까지 걸리는 시간 (초)")]
    [Min(0.1f)] public float burnDuration = 20f;
    [Tooltip("다 타서 사라질 때 떨어뜨릴 아이템 (dropTable 대신 사용)")]
    public DropTable burnDropTable;
}
