using System.Collections.Generic;
using UnityEngine;

// 몬스터 공격에 맞으면 플레이어를 중심으로 사방에 음파를 냄 (가까운 적 기절, 금속 물체에 닿으면 연쇄)
// 환경 피해·피해 없는 공격에는 반응하지 않고, 한 번 울리면 재사용 대기 시간 동안 다시 울리지 않음
[CreateAssetMenu(fileName = "SoundWaveOnHitEffect", menuName = "Item/Equipment Effect/Sound Wave On Hit")]
public class SoundWaveOnHitEffect : EquipmentEffect
{
    [SerializeField] private SoundWaveSetting setting;
    [Tooltip("음파 세기 (1 = 강타 음파와 같은 길이)")]
    [SerializeField, Range(0.1f, 1f)] private float power = 0.5f;
    [Tooltip("재사용 대기 시간 (초)")]
    [SerializeField, Min(0f)] private float cooldown = 6f;

    // 장착한 플레이어별 다음에 울릴 수 있는 시각 (에셋을 함께 쓰므로 소유자별로 보관)
    private readonly Dictionary<PlayerEquipment, float> readyAt = new();

    public override void OnHit(PlayerEquipment owner, AttackData attack)
    {
        if (setting == null || attack.environmental || attack.damage <= 0) return;
        if (readyAt.TryGetValue(owner, out float ready) && Time.time < ready) return;

        readyAt[owner] = Time.time + cooldown;
        Vector3 origin = owner.transform.position;
        Sfx.Play(setting.waveClip, origin, Mathf.Lerp(0.5f, 1f, power));
        SoundWave.Spawn(setting, origin, owner.transform.forward, power, 0, Resonant.NewChainId(), owner.transform, 360f);
    }
}
