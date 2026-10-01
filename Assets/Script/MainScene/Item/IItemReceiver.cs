// ItemCollector가 획득한 아이템을 넘겨받는 대상 (플레이어 인벤토리, 상자, 수집 몬스터 등)
public interface IItemReceiver
{
    // 받을 수 있는 만큼만 받고 실제로 받은 개수를 반환 (가득 차면 0)
    int TryAdd(ItemData item, int count);
}
