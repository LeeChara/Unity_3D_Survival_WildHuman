// 기절할 수 있는 대상 (플레이어, 몬스터). 기절 중에는 이동·공격 등 아무 행동도 하지 못함
public interface IStunnable
{
    // 기절 표시(StunIndicator)가 확인
    bool IsStunned { get; }

    // 이미 기절 중이면 남은 시간과 새 시간 중 긴 쪽을 씀
    void Stun(float duration);
}
