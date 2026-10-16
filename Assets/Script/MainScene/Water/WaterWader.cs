using UnityEngine;

// 물 칸 위를 걷는 생물 (플레이어, 몬스터)
// 발밑이 물 칸이면 평소 이동(걷기·달리기, 배회·추적·후퇴)이 느려짐. 구르기·공격 돌진·넉백은 그대로
// 젖음 상태와는 별개인 지형 효과라서, 물에서 나오면 젖어 있어도 바로 원래 속도로 돌아옴
// 물가를 드나들 때 늦지 않도록 물 호수의 적시기 주기(WaterLake)를 쓰지 않고 물리 프레임마다 직접 확인
public class WaterWader : MonoBehaviour
{
    [SerializeField] private WaterSetting setting;

    public bool InWater { get; private set; }
    // 이동 속도에 곱할 값 (물 위면 WaterSetting.waterSpeedMultiplier, 아니면 1)
    public float SpeedMultiplier => InWater ? setting.waterSpeedMultiplier : 1f;

    private void OnDisable()
    {
        InWater = false;
    }

    private void FixedUpdate()
    {
        InWater = HazardQuery.IsInside(transform.position, setting.waterLayers);
    }
}
