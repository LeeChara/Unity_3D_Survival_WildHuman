using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

// 월드 세이브 파일 입출력 (persistentDataPath/Saves/<월드ID>/)
// meta.json: 목록 화면용 요약, world.json: 실제 상태, *.bak: 직전 저장본
// 새 내용을 .tmp에 다 쓴 뒤 기존 파일을 .bak으로 돌리고 교체하므로, 쓰는 도중 꺼져도 이전 파일이 남음
// 읽기에 실패하면 .bak으로 한 번 더 시도
public static class SaveSystem
{
    public const int SaveVersion = 1;

    private const string MetaFile = "meta.json";
    private const string WorldFile = "world.json";
    private const string BackupSuffix = ".bak";
    private const string TempSuffix = ".tmp";

    // 백그라운드 쓰기와 메인 스레드 쓰기·삭제가 겹치지 않도록 잠금
    private static readonly object fileLock = new();

    // Application.persistentDataPath는 메인 스레드에서만 읽을 수 있으므로 처음 접근할 때 캐시
    private static string root;
    private static string Root => root ??= Path.Combine(Application.persistentDataPath, "Saves");

    public static string NewWorldId() => Guid.NewGuid().ToString("N");

    // 마지막 플레이 시각이 최근인 순서
    public static List<WorldMeta> ListWorlds()
    {
        var list = new List<WorldMeta>();
        if (!Directory.Exists(Root)) return list;

        foreach (string dir in Directory.GetDirectories(Root))
        {
            WorldMeta meta = ReadWithBackup<WorldMeta>(Path.Combine(dir, MetaFile));
            // 메타가 깨진 폴더는 목록에서 제외 (파일은 지우지 않음)
            if (meta == null) continue;

            meta.id = Path.GetFileName(dir);
            list.Add(meta);
        }

        list.Sort((a, b) => b.lastPlayedAt.CompareTo(a.lastPlayedAt));
        return list;
    }

    public static WorldMeta ReadMeta(string id)
    {
        return ReadWithBackup<WorldMeta>(Path.Combine(Root, id, MetaFile));
    }

    public static WorldSaveData ReadWorld(string id)
    {
        return ReadWithBackup<WorldSaveData>(Path.Combine(Root, id, WorldFile));
    }

    // 메인 스레드에서 바로 저장 (게임 종료 직전처럼 기다려야 하는 경우)
    public static void Write(WorldMeta meta, WorldSaveData data)
    {
        string dir = Path.Combine(Root, meta.id);
        string metaJson = JsonConvert.SerializeObject(meta, Formatting.Indented);
        string worldJson = JsonConvert.SerializeObject(data);

        lock (fileLock)
        {
            Directory.CreateDirectory(dir);
            // 월드를 먼저 써야 메타만 갱신되고 월드가 옛날 것으로 남는 일이 없음
            WriteFile(Path.Combine(dir, WorldFile), worldJson);
            WriteFile(Path.Combine(dir, MetaFile), metaJson);
        }
    }

    // 직렬화와 파일 쓰기를 백그라운드에서 처리 (자동 저장 끊김 방지)
    // data는 이후 게임 진행으로 바뀌지 않는 복사본이어야 함
    public static Task WriteAsync(WorldMeta meta, WorldSaveData data)
    {
        _ = Root;
        return Task.Run(() => Write(meta, data));
    }

    public static void Delete(string id)
    {
        string dir = Path.Combine(Root, id);

        lock (fileLock)
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    private static void WriteFile(string path, string content)
    {
        string temp = path + TempSuffix;
        string backup = path + BackupSuffix;

        File.WriteAllText(temp, content);

        if (File.Exists(path))
        {
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(path, backup);
        }
        File.Move(temp, path);
    }

    private static T ReadWithBackup<T>(string path) where T : class
    {
        T result = ReadJson<T>(path);
        if (result != null) return result;

        string backup = path + BackupSuffix;
        result = ReadJson<T>(backup);
        if (result != null) Debug.LogWarning($"[SaveSystem] {path} 읽기에 실패해 백업 파일로 불러옴");
        return result;
    }

    private static T ReadJson<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;

        try
        {
            lock (fileLock)
            {
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] {path} 읽기 실패: {e.Message}");
            return null;
        }
    }
}
