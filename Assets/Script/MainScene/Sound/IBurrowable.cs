// 땅속에 숨을 수 있는 몬스터 (예: 매복 중인 Roseviper). 땅속에 있을 때 음파를 맞으면 즉시 길게 기절
public interface IBurrowable
{
    bool IsBurrowed { get; }
}
