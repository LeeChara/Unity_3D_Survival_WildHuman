using UnityEngine;

// 공용 효과음 목록 (Resources/Audio/SfxSetting 하나만 둠)
// 개체마다 다른 소리는 각자의 데이터에 두고(PropData·MonsterData·BiomeData), 비어 있으면 여기의 기본값을 씀
// 필드 이름이 곧 효과음 에셋 이름 (Tools > Sfx > Build Placeholder Cues가 이름으로 연결)
[CreateAssetMenu(fileName = "SfxSetting", menuName = "Audio/SfxSetting")]
public class SfxSetting : ScriptableObject
{
    private const string ResourcePath = "Audio/SfxSetting";
    private static SfxSetting instance;

    // 에셋이 없으면 null (호출하는 쪽은 Sfx.Play가 null cue를 무시하므로 ?.로 읽음)
    public static SfxSetting Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<SfxSetting>(ResourcePath);
            return instance;
        }
    }

    [Header("플레이어")]
    [Tooltip("바이옴에 발소리가 없을 때")]
    public SfxCue footstep;
    public SfxCue footstepWater;
    public SfxCue footstepIce;
    [Tooltip("발소리 사이 거리 (m)")]
    [Min(0.1f)] public float footstepStride = 1.1f;
    public SfxCue swing;
    [Tooltip("강타 무기를 휘두를 때")]
    public SfxCue swingHeavy;
    public SfxCue dodge;
    public SfxCue playerHurt;
    public SfxCue playerDeath;
    public SfxCue respawn;
    public SfxCue playerStun;
    public SfxCue eat;
    public SfxCue pickup;
    public SfxCue throwItem;
    public SfxCue place;
    public SfxCue bucketScoop;
    public SfxCue bucketPour;
    public SfxCue equipOn;
    public SfxCue equipOff;
    public SfxCue hotbarSelect;
    [Tooltip("허기가 바닥나기 직전·굶주리기 시작할 때")]
    public SfxCue hungerWarning;
    [Range(0f, 1f)] public float hungerWarningRatio = 0.2f;
    [Tooltip("체력이 낮을 때 반복")]
    public SfxCue heartbeat;
    [Range(0f, 1f)] public float heartbeatRatio = 0.25f;
    [Min(0.2f)] public float heartbeatInterval = 0.9f;

    [Header("채집 (PropData에 소리가 없으면 약한 도구로 구분)")]
    public SfxCue hitWood;
    public SfxCue breakWood;
    public SfxCue hitStone;
    public SfxCue breakStone;
    public SfxCue hitPlant;
    public SfxCue breakPlant;
    [Tooltip("도구 티어가 부족해 피해가 들어가지 않을 때")]
    public SfxCue hitBlocked;
    public SfxCue hitStructure;
    public SfxCue breakStructure;
    [Tooltip("가시·선인장에 찔릴 때")]
    public SfxCue prick;

    [Header("몬스터 (MonsterData에 소리가 없을 때)")]
    public SfxCue monsterHurt;
    public SfxCue monsterDeath;
    public SfxCue monsterDetect;
    public SfxCue monsterWindup;
    public SfxCue monsterAttack;
    public SfxCue monsterStun;

    [Header("불·물")]
    public SfxCue ignite;
    public SfxCue burnLoop;
    [Tooltip("불이 꺼지거나 젖은 것이 마를 때 (치익)")]
    public SfxCue extinguish;
    public SfxCue splash;
    public SfxCue fuseLoop;
    public SfxCue explosion;

    [Header("제작·설치물")]
    public SfxCue craft;
    public SfxCue cookAdd;
    public SfxCue cookDone;
    public SfxCue smeltLoop;
    public SfxCue smeltDone;
    public SfxCue chestOpen;
    public SfxCue chestClose;

    [Header("UI")]
    public SfxCue uiClick;
    public SfxCue uiHover;
    [Tooltip("누를 수 없는 버튼을 눌렀을 때")]
    public SfxCue uiDeny;
    public SfxCue uiOpen;
    public SfxCue uiClose;
    public SfxCue slotPick;
    public SfxCue slotDrop;
    public SfxCue mapOpen;
    public SfxCue pageFlip;
    public SfxCue saveDone;
    public SfxCue worldCreate;
    [Tooltip("타이틀 화면 장식을 클릭할 때")]
    public SfxCue menuPoke;
    public SfxCue menuBreak;

    [Header("월드")]
    public SfxCue dawn;
    public SfxCue dusk;
    public SfxCue thunder;
    [Tooltip("비가 이 세기 이상일 때 가끔 천둥")]
    [Range(0f, 1f)] public float thunderRainFactor = 0.7f;
    [Tooltip("천둥 사이 시간 범위 (초)")]
    public Vector2 thunderInterval = new Vector2(25f, 70f);
    public SfxCue ambientDay;
    public SfxCue ambientNight;
    [Range(0f, 1f)] public float ambientVolume = 0.35f;
    public SfxCue campfireLoop;
    public SfxCue lavaLoop;
}
