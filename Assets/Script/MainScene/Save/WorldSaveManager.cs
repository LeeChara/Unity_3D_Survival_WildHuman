using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// 월드 저장·복원 총괄 (자동 저장, 나갈 때 저장)
// 다른 컴포넌트의 Start(시작 아이템 지급, 시작 지점 이동 등)가 끝난 뒤 저장된 상태로 덮어쓰도록 늦게 실행
// 월드 선택 없이 MainScene을 바로 실행한 임시 월드는 저장하지 않음
[DefaultExecutionOrder(100)]
public class WorldSaveManager : MonoBehaviour
{
    [SerializeField] private SaveRegistry registry;
    [Tooltip("자동 저장 간격")]
    [SerializeField] private GameSetting gameSetting;
    [SerializeField] private WorldState worldState;
    [SerializeField] private SaveableEntity player;
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("월드 목록에 표시할 일차")]
    [SerializeField] private DayNightCycle dayNightCycle;

    public static WorldSaveManager Instance { get; private set; }

    public bool CanSave => WorldSession.HasWorld;

    // 저장 표시 UI용 (완료는 파일 쓰기가 끝난 뒤 메인 스레드에서 알림)
    public event Action SaveStarted;
    public event Action SaveFinished;

    private ISaveParticipant[] participants;
    private Task writing;
    private float autosaveTimer;
    // 마지막 저장 이후 흐른 플레이 시간 (일시정지 중에는 멈춤)
    private float unsavedPlayTime;
    private bool savedForExit;

    private void Awake()
    {
        Instance = this;
        SaveRegistry.Active = registry;
    }

    private void Start()
    {
        participants = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISaveParticipant>().ToArray();

        if (CanSave) Restore(WorldSession.Data);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (writing != null && writing.IsCompleted) FinishWriting();

        if (!CanSave) return;

        unsavedPlayTime += Time.deltaTime;
        autosaveTimer += Time.deltaTime;
        if (autosaveTimer >= gameSetting.save.autosaveInterval) SaveNow(false);
    }

    // 창을 닫거나 에디터 플레이를 멈출 때
    private void OnApplicationQuit()
    {
        if (!savedForExit) SaveNow(true);
    }

    // wait: 파일 쓰기가 끝날 때까지 기다림 (씬 이동·종료 직전)
    public void SaveNow(bool wait)
    {
        if (!CanSave) return;

        if (writing != null)
        {
            // 자동 저장끼리 겹치면 이번 것은 건너뜀 (직전 저장이 곧 끝나므로)
            if (!wait) return;
            WaitForWriting();
        }

        WorldMeta meta = WorldSession.Meta;
        meta.lastPlayedAt = DateTime.UtcNow.Ticks;
        meta.playTime += unsavedPlayTime;
        meta.dayCount = dayNightCycle.DayCount;
        unsavedPlayTime = 0f;
        autosaveTimer = 0f;

        WorldSaveData data = Capture();

        SaveStarted?.Invoke();
        writing = SaveSystem.WriteAsync(meta, data);

        if (wait) WaitForWriting();
    }

    // 나가기 직전 저장 (이후 OnApplicationQuit에서 다시 저장하지 않음)
    public void SaveForExit()
    {
        SaveNow(true);
        savedForExit = true;
    }

    private WorldSaveData Capture()
    {
        var data = new WorldSaveData
        {
            saveVersion = SaveSystem.SaveVersion,
            map = worldState.Map.Clone(),
            player = CapturePlayer(),
        };

        foreach (var participant in participants)
        {
            participant.Capture(data);
        }
        return data;
    }

    // 사망 중이면 부활한 상태로 저장 (체력·허기는 각 컴포넌트가 가득 찬 값으로 저장)
    private EntityRecord CapturePlayer()
    {
        EntityRecord record = player.Capture();
        if (playerHealth.IsDead)
        {
            Vector3 spawn = playerHealth.SpawnPoint;
            record.x = spawn.x;
            record.y = spawn.y;
            record.z = spawn.z;
        }
        return record;
    }

    private void Restore(WorldSaveData data)
    {
        // 새 월드면 플레이어 기록이 없음 (씬 기본값과 시작 아이템으로 시작)
        if (data.player != null)
        {
            player.LoadData(data.player);
            playerHealth.WarpTo(data.player.Position());
        }

        foreach (var participant in participants)
        {
            participant.Restore(data);
        }
    }

    private void WaitForWriting()
    {
        try
        {
            writing.Wait();
        }
        catch (AggregateException)
        {
            // 실패 내용은 FinishWriting에서 로그로 남김
        }
        FinishWriting();
    }

    private void FinishWriting()
    {
        if (writing.IsFaulted) Debug.LogError($"[WorldSaveManager] 저장 실패: {writing.Exception?.GetBaseException()}");

        writing = null;
        SaveFinished?.Invoke();
    }
}
