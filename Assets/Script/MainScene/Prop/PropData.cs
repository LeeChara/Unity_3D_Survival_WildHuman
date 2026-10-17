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
    [Tooltip("무기로 공격받을 때 피해 감소를 적용할지 (끄면 무기 피해를 그대로 받음, 예: 덤불)")]
    public bool reduceWeaponDamage = true;
    [Tooltip("파괴될 때 떨어뜨릴 아이템")]
    public DropTable dropTable;
    [Header("이펙트 (비워두면 재생하지 않음)")]
    [Tooltip("공격받을 때마다 뿌리는 파티클 (예: 나무 조각, 돌 파편)")]
    public EffectData hitEffect;
    [Tooltip("파괴될 때 뿌리는 파티클")]
    public EffectData destroyEffect;
    [Tooltip("파티클에 곱할 색 (공용 파편 그림을 자원 색으로 물들임)")]
    public Color effectColor = Color.white;

    [Header("효과음 (비워두면 약한 도구로 구분: 도끼 = 나무, 곡괭이 = 돌, 그 외 = 풀)")]
    public SfxCue hitCue;
    public SfxCue breakCue;

    public SfxCue HitCue => SfxCue.Or(hitCue, SfxSetting.Instance == null ? null : toolWeakness.tool switch
    {
        ToolType.Axe => SfxSetting.Instance.hitWood,
        ToolType.Pickaxe => SfxSetting.Instance.hitStone,
        _ => SfxSetting.Instance.hitPlant,
    });

    public SfxCue BreakCue => SfxCue.Or(breakCue, SfxSetting.Instance == null ? null : toolWeakness.tool switch
    {
        ToolType.Axe => SfxSetting.Instance.breakWood,
        ToolType.Pickaxe => SfxSetting.Instance.breakStone,
        _ => SfxSetting.Instance.breakPlant,
    });

    // 아래 프리팹들은 모두 자원 프리팹 (PropHealth가 있어야 함). 시간은 월드 시간(초) 기준이라 청크가 언로드돼도 흐름
    [Header("재생")]
    [Tooltip("그 자리에서 growTime이 지나면 이 자원으로 바뀜 (예: 빈 덤불 → 덤불, 밑동 → 나무). 비워두면 바뀌지 않음")]
    public GameObject growInto;
    [Min(0f)] public float growTime = 300f;
    [Tooltip("파괴되면 그 자리에 바로 남는 자원 (예: 나무 → 밑동). 타는 중에 쓰러지면 남기지 않음")]
    public GameObject leaveBehind;
    [Tooltip("파괴되면 regrowDelay 뒤 같은 청크의 다른 위치에 생기는 자원 (예: 덤불 → 빈 덤불). 비워두면 다시 생기지 않음")]
    public GameObject regrowAs;
    [Min(0f)] public float regrowDelay = 300f;
    [Tooltip("재생·지진으로 새로 생길 때 다른 자원과 띄울 최소 거리")]
    [Min(0f)] public float spacing = 2f;
}
