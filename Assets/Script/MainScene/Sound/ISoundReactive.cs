// 음파(SoundWave)가 지나갈 때 반응하는 대상 (예: 금속 물체는 다시 음파를 내고, 생물은 밀려나며 기절)
// 음파는 이 인터페이스가 있는 대상에게만 반응을 일으킴 (피해·피격 반짝임 없음)
public interface ISoundReactive
{
    void OnSoundWave(SoundWave wave);
}
