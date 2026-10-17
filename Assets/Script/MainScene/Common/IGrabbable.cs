// 보스의 잡기에 붙잡힐 수 있는 대상 (현재는 플레이어)
public interface IGrabbable
{
    bool IsGrabbed { get; }

    // 붙잡기 시도 (회피 중 등 붙잡을 수 없으면 false)
    bool TryGrab();

    // 붙잡힌 동안 잡은 쪽이 매 프레임 위치를 정함
    void HoldAt(UnityEngine.Vector3 position);

    // 내려놓음 (던지기는 내려놓은 뒤 넉백으로)
    void Release();
}
