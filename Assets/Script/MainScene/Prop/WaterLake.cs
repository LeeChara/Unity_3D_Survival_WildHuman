using UnityEngine;

// 물 호수: 칸 지도 호수(TileLake) 위에 있는 생물을 계속 적심
// 걸어 들어갈 수 있고 느려지지 않음 (젖음 자체에는 효과가 없음)
// 물 칸 위에 있는 동안 젖음 시간을 계속 다시 채우므로, 물에서 나온 뒤부터 젖음 시간이 줄어듦
// 타는 생물이 들어오면 바로 불이 꺼짐 (Wettable.Soak)
// 피격이 아니라 직접 적시므로 피해·피격 반짝임 없음
// 설치물은 물 위에 놓을 수 없어서(PlacementManager.hazardLayers) 생물만 봄
// 트리거 대신 거리 검사를 쓰는 이유: 물 칸 판정이 트리거 모양(구)과 다르고, 들어와 멈춘 대상도 계속 적셔야 함
public class WaterLake : TileLake
{
    private const int BufferSize = 64;

    [Header("적심")]
    [Tooltip("물 위에 있는 대상을 확인하는 간격 (초)")]
    [SerializeField, Min(0.05f)] private float soakInterval = 0.25f;
    [Tooltip("적실 대상 레이어 (플레이어, 몬스터)")]
    [SerializeField] private LayerMask targetLayers;

    private static readonly Collider[] buffer = new Collider[BufferSize];
    private float timer;

    protected override void OnEnable()
    {
        base.OnEnable();
        timer = 0f;
    }

    protected override void Update()
    {
        base.Update();
        if (!IsBuilt) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = soakInterval;

        Vector3 center = transform.position;
        int count = Physics.OverlapSphereNonAlloc(center, Current.radius, buffer, targetLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Wettable wettable = buffer[i].GetComponentInParent<Wettable>();
            if (wettable != null && Contains(wettable.transform.position)) wettable.Soak();
        }
    }
}
