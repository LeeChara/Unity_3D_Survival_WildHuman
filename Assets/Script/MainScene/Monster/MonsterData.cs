using UnityEngine;

[CreateAssetMenu(fileName = "MonsterData", menuName = "Monster/MonsterData")]
public class MonsterData : ScriptableObject
{
    public string monsterName;
    [Tooltip("마우스를 올렸을 때 표시할 이름 (비워두면 표시하지 않음)")]
    public string displayName;
    [Tooltip("도감에 표시할 설명 (특징, 행동 패턴 등)")]
    [TextArea] public string description;
    [Tooltip("도감 등 UI에 표시할 전신 이미지 (몬스터 프리팹은 부위별 스프라이트로 나뉘어 있음)")]
    public Sprite portrait;

    [Header("변종")]
    [Tooltip("바이옴 변종이면 원본 종 (비워두면 자기 자신이 원본). 적대 관계는 원본 종 기준으로 판정")]
    public MonsterData species;

    [Header("적대 대상")]
    [Tooltip("원본 종으로 지정 (그 종의 변종도 모두 적대 대상이 됨)")]
    public MonsterData[] hostileTargets;

    [Header("기본 능력치")]
    public float maxHealth;
    [Tooltip("체력 비율이 이 값 이하가 되면 땀방울 이펙트 표시")]
    [Range(0f, 1f)] public float lowHealthRatio = 0.3f;

    [Header("Idle")]
    public float moveSpeed = 1f;
    public float wanderRange = 3f;
    public float wanderCooldown = 3f;

    [Header("Chase")]
    public float chaseSpeed = 3f;
    [Tooltip("목표물 감지 반경")]
    public float chaseRange = 10f;
    [Tooltip("추격 포기 반경. 감지 반경보다 넓어야 경계에서 인식/해제가 반복되지 않음")]
    public float chaseGiveUpRange = 15f;

    [Header("Windup")]
    public float windupRange = 2f;
    public float windupDuration = 1f;

    [Header("Attack")]
    public float attackSpeed = 6f;
    public float attackDuration = 0.5f;
    public AttackData attack = new AttackData { damage = 10, knockbackDistance = 1f };

    [Header("Recover")]
    public float recoverDuration = 1f;

    [Header("Knockback")]
    [Tooltip("피격 시 덜 밀리는 정도 (1이면 밀리지 않음)")]
    [Range(0f, 1f)] public float knockbackResistance = 0f;
    [Tooltip("Windup/Attack 중 피격 시 넉백되지 않고 공격을 유지")]
    public bool superArmorWhileAttacking = false;

    [Header("Drop")]
    [Tooltip("사망 시 드랍 (ItemDropOnDeath가 있는 프리팹에만 적용)")]
    public DropTable dropTable;

    [Header("효과음 (비워두면 원본 종의 소리, 그것도 없으면 공용 기본값)")]
    [Tooltip("목표를 발견했을 때 (Roseviper는 땅속으로 숨는 소리)")]
    public SfxCue detectCue;
    public SfxCue windupCue;
    [Tooltip("공격 시작 (Stoneotter는 돌을 던질 때마다)")]
    public SfxCue attackCue;
    [Tooltip("공격하는 동안 반복 (예: Shieldboar 돌진 중 발굽)")]
    public SfxCue attackLoopCue;
    public SfxCue hurtCue;
    public SfxCue deathCue;
    [Tooltip("공격을 시작할 때 포효 (예: Shieldboar 돌진 출발). 비워두면 포효하지 않음")]
    public SfxCue roarCue;
    [Tooltip("평소(배회·대기)에 가끔 내는 울음소리")]
    public SfxCue callCue;
    [Tooltip("울음소리 사이 시간 범위 (초)")]
    public Vector2 callInterval = new Vector2(10f, 25f);
    [Tooltip("이 몬스터의 모든 효과음 음 높이 배율 (변종을 원본과 다르게 들리게, 예: 설원 0.9)")]
    [Range(0.5f, 2f)] public float sfxPitch = 1f;

    public MonsterData Species => species != null ? species : this;

    public SfxCue DetectCue => SfxCue.Or(SfxCue.Or(detectCue, Species.detectCue), SfxSetting.Instance?.monsterDetect);
    public SfxCue WindupCue => SfxCue.Or(SfxCue.Or(windupCue, Species.windupCue), SfxSetting.Instance?.monsterWindup);
    public SfxCue AttackLoopCue => SfxCue.Or(attackLoopCue, Species.attackLoopCue);
    public SfxCue AttackCue => SfxCue.Or(SfxCue.Or(attackCue, Species.attackCue), SfxSetting.Instance?.monsterAttack);
    public SfxCue RoarCue => SfxCue.Or(roarCue, Species.roarCue);
    public SfxCue CallCue => SfxCue.Or(callCue, Species.callCue);
    public SfxCue HurtCue => SfxCue.Or(SfxCue.Or(hurtCue, Species.hurtCue), SfxSetting.Instance?.monsterHurt);
    public SfxCue DeathCue => SfxCue.Or(SfxCue.Or(deathCue, Species.deathCue), SfxSetting.Instance?.monsterDeath);

    // 변종끼리도 원본 종이 같으면 같은 대상으로 봄
    public bool IsHostileTo(MonsterData other)
    {
        if (other == null || hostileTargets == null) return false;

        MonsterData otherSpecies = other.Species;
        foreach (MonsterData hostileTarget in hostileTargets)
        {
            if (hostileTarget != null && hostileTarget.Species == otherSpecies) return true;
        }
        return false;
    }

    private void OnValidate()
    {
        // 포기 반경이 감지 반경보다 좁으면 감지 직후 바로 포기하게 되므로 보정
        chaseGiveUpRange = Mathf.Max(chaseGiveUpRange, chaseRange);
    }
}
