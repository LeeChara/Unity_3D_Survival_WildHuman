// 화염원(FireSource)에 닿았을 때 반응하는 대상 (예: 가연물은 불이 붙고, 화약통은 도화선에 불이 붙음)
// 화염원은 이 인터페이스가 있는 대상에게만 불을 옮김 → 타지 않는 대상(바위, 모닥불, 재련로)은 화염원의 영향을 받지 않음
// 피해·피격 반짝임 없이 반응만 일어남 (직접 타격·폭발처럼 실제 공격은 Health.TakeHit으로 따로 처리)
public interface IFireReactive
{
    void OnFireContact();
}
