using UnityEngine;

// 플레이어 공격·회피 타이밍과 연계(선입력·캔슬) 설정을 한곳에서 조정
// PlayerAttack, PlayerDodge, PlayerInputBuffer가 같은 에셋을 참조
[CreateAssetMenu(fileName = "PlayerCombatSetting", menuName = "Player/PlayerCombatSetting")]
public class PlayerCombatSetting : ScriptableObject
{
    [Header("공격")]
    [Tooltip("휘두르기 전 준비 시간(초)")]
    [Min(0f)] public float attackWindup = 0.2f;
    [Tooltip("판정이 켜져 있는 시간(초)")]
    [Min(0f)] public float attackActive = 0.3f;
    [Tooltip("판정이 꺼진 뒤 경직 시간(초)")]
    [Min(0f)] public float attackRecover = 0.3f;
    [Tooltip("공격 시작부터 다음 공격까지 최소 간격(초, 0이면 쿨다운 없음)")]
    [Min(0f)] public float attackCooldown = 0f;

    [Header("회피")]
    [Min(0f)] public float dodgeDistance = 5f;
    [Tooltip("회피 이동 시간(초)")]
    [Min(0.01f)] public float dodgeDuration = 0.7f;
    [Tooltip("회피 시작부터 다음 회피까지 최소 간격(초)")]
    [Min(0f)] public float dodgeCooldown = 1.4f;

    [Header("선입력")]
    [Tooltip("행동 중에 누른 공격·회피를 기억해 두는 시간(초). 이 안에 행동이 가능해지면 자동 실행")]
    [Min(0f)] public float inputBufferDuration = 0.2f;

    [Header("캔슬")]
    [Tooltip("경직이 시작되고 이 시간(초)이 지나면 이동으로 경직을 끊을 수 있음 (0이면 경직 내내 가능)")]
    [Min(0f)] public float recoverMoveCancelAfter = 0f;
    [Tooltip("경직이 시작되고 이 시간(초)이 지나면 다음 공격으로 경직을 끊을 수 있음 (0이면 경직 내내 가능)")]
    [Min(0f)] public float recoverAttackCancelAfter = 0f;
    [Tooltip("공격 도중(준비·판정·경직 전 구간) 회피로 공격을 끊을 수 있는지")]
    public bool dodgeCancelsAttack = true;
}
