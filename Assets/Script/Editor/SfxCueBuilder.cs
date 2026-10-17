using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// 효과음 에셋을 만들고 이름으로 연결 (개발용)
// - 합성 임시음(Tools/SfxSynth/sfx_synth.py가 만든 wav)에서 효과음 에셋을 만들고, 같은 이름에 _번호가 붙은 wav는 변형으로 묶음
// - SfxSetting의 같은 이름 필드, "<MonsterData·BossData 이름>_<필드 이름에서 Cue를 뗀 것>", "<보스 패턴 에셋 이름>_<같은 규칙>",
//   "footstep_<바이옴 id>"에 연결 (예: Shieldboar_attackLoop, SunBear_summon, SunBearCharge_runLoop)
// - 이미 다른 효과음이 연결된 칸, 직접 교체한 효과음(클립이 임시음 폴더 밖에 있음)은 건드리지 않음
// - Custom 폴더에 효과음 이름과 같은 파일을 넣고 ApplyCustomFolder를 실행하면 한꺼번에 교체
public static class SfxCueBuilder
{
    public const string PlaceholderRoot = "Assets/Audio/Sfx/Placeholder/Generated";
    public const string CustomRoot = "Assets/Audio/Sfx/Custom";
    public const string CueRoot = "Assets/Audio/Sfx/Cues";
    private const string SettingPath = "Assets/Resources/Audio/SfxSetting.asset";
    private static readonly Regex VariantSuffix = new(@"_\d+$");

    [MenuItem("Tools/Sfx/Build Placeholder Cues")]
    public static void Build()
    {
        Dictionary<string, List<string>> groups = GroupClips(PlaceholderRoot, out Dictionary<string, string> categories);

        var cues = new Dictionary<string, SfxCue>();
        int kept = 0;
        foreach (var pair in groups)
        {
            cues[pair.Key] = BuildCue(pair.Key, categories[pair.Key], pair.Value, out bool replaced);
            if (replaced) kept++;
        }

        SfxSetting setting = AssetDatabase.LoadAssetAtPath<SfxSetting>(SettingPath);
        if (setting == null)
        {
            setting = ScriptableObject.CreateInstance<SfxSetting>();
            AssetDatabase.CreateAsset(setting, SettingPath);
        }

        int linked = LinkFields(setting, cues, name => name);
        foreach (string guid in AssetDatabase.FindAssets("t:MonsterData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<MonsterData>(AssetDatabase.GUIDToAssetPath(guid));
            linked += LinkFields(data, cues, field => field.EndsWith("Cue") ? $"{data.name}_{field[..^3]}" : null);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:BossPattern"))
        {
            var pattern = AssetDatabase.LoadAssetAtPath<BossPattern>(AssetDatabase.GUIDToAssetPath(guid));
            linked += LinkFields(pattern, cues, field => field.EndsWith("Cue") ? $"{pattern.name}_{field[..^3]}" : null);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:BiomeData"))
        {
            var biome = AssetDatabase.LoadAssetAtPath<BiomeData>(AssetDatabase.GUIDToAssetPath(guid));
            linked += LinkFields(biome, cues, field => field == "footstepCue" ? $"footstep_{biome.id}" : null);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SfxCueBuilder] 효과음 {cues.Count}개 갱신 (직접 교체한 {kept}개는 유지), 빈 칸 {linked}개 연결");
    }

    // Custom 폴더의 파일을 이름이 같은 효과음에 넣음. 이름이 맞지 않는 파일은 목록으로 알려 줌
    [MenuItem("Tools/Sfx/Apply Custom Folder")]
    public static void ApplyCustomFolder()
    {
        EnsureFolder(CustomRoot);
        Dictionary<string, List<string>> groups = GroupClips(CustomRoot, out _);
        Dictionary<string, SfxCue> cues = FindAllCues();

        int applied = 0;
        var unmatched = new List<string>();
        foreach (var pair in groups)
        {
            if (!cues.TryGetValue(pair.Key, out SfxCue cue))
            {
                unmatched.Add(pair.Key);
                continue;
            }
            SetClips(cue, LoadClips(pair.Value, IsLoopName(pair.Key)), "Custom 폴더 적용");
            applied++;
        }

        AssetDatabase.SaveAssets();
        string message = $"효과음 {applied}개를 교체했습니다.";
        if (unmatched.Count > 0) message += $"\n\n이름이 맞는 효과음이 없는 파일 ({unmatched.Count}개):\n{string.Join(", ", unmatched)}";
        EditorUtility.DisplayDialog("Custom 폴더 적용", message, "확인");
    }

    // 직접 교체한 효과음을 합성 임시음으로 되돌림 (임시음이 없으면 false)
    public static bool RevertToPlaceholder(SfxCue cue)
    {
        Dictionary<string, List<string>> groups = GroupClips(PlaceholderRoot, out _);
        if (!groups.TryGetValue(cue.name, out List<string> paths)) return false;

        SetClips(cue, LoadClips(paths, IsLoopName(cue.name)), "임시음으로 되돌리기");
        AssetDatabase.SaveAssets();
        return true;
    }

    // 클립이 하나도 없거나 모두 임시음 폴더에 있으면 임시음
    public static bool IsPlaceholder(SfxCue cue)
    {
        if (cue.IsEmpty) return true;
        foreach (AudioClip clip in cue.clips)
        {
            if (clip != null && !AssetDatabase.GetAssetPath(clip).StartsWith(PlaceholderRoot)) return false;
        }
        return true;
    }

    public static void SetClips(SfxCue cue, AudioClip[] clips, string undoName)
    {
        Undo.RecordObject(cue, undoName);
        cue.clips = clips;
        EditorUtility.SetDirty(cue);
    }

    public static AudioClip[] LoadClips(List<string> paths, bool loop)
    {
        paths.Sort();
        var clips = new List<AudioClip>();
        foreach (string path in paths)
        {
            ConfigureImport(path, loop);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) clips.Add(clip);
        }
        return clips.ToArray();
    }

    public static bool IsLoopName(string name) => name.EndsWith("Loop") || name.StartsWith("ambient");

    // 효과음 이름(파일 이름에서 _번호를 뗀 것) → 파일 경로들, 이름 → 바로 위 폴더 이름(분류)
    public static Dictionary<string, List<string>> GroupClips(string root, out Dictionary<string, string> categories)
    {
        var groups = new SortedDictionary<string, List<string>>();
        categories = new Dictionary<string, string>();
        if (!AssetDatabase.IsValidFolder(root)) return new Dictionary<string, List<string>>();

        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = VariantSuffix.Replace(Path.GetFileNameWithoutExtension(path), "");
            if (!groups.TryGetValue(name, out List<string> list)) groups[name] = list = new List<string>();
            list.Add(path);
            categories[name] = Path.GetFileName(Path.GetDirectoryName(path));
        }
        return new Dictionary<string, List<string>>(groups);
    }

    public static Dictionary<string, SfxCue> FindAllCues()
    {
        var cues = new Dictionary<string, SfxCue>();
        foreach (string guid in AssetDatabase.FindAssets("t:SfxCue", new[] { CueRoot }))
        {
            var cue = AssetDatabase.LoadAssetAtPath<SfxCue>(AssetDatabase.GUIDToAssetPath(guid));
            if (cue != null) cues[cue.name] = cue;
        }
        return cues;
    }

    private static SfxCue BuildCue(string name, string category, List<string> clipPaths, out bool replaced)
    {
        bool loop = IsLoopName(name);
        bool ui = category == "UI";

        string folder = $"{CueRoot}/{category}";
        EnsureFolder(folder);
        string path = $"{folder}/{name}.asset";
        SfxCue cue = AssetDatabase.LoadAssetAtPath<SfxCue>(path);
        if (cue == null)
        {
            cue = ScriptableObject.CreateInstance<SfxCue>();
            // 반복음은 겹쳐 틀 일이 없고, UI는 음 높이가 흔들리면 어색하므로 좁게
            if (loop) cue.pitchRange = new Vector2(0.97f, 1.03f);
            if (ui) cue.pitchRange = new Vector2(0.98f, 1.02f);
            if (loop) cue.minInterval = 0f;
            AssetDatabase.CreateAsset(cue, path);
        }

        if (string.IsNullOrEmpty(cue.description)) cue.description = SfxDescriptions.Describe(name);

        replaced = !IsPlaceholder(cue);
        if (!replaced) cue.clips = LoadClips(clipPaths, loop);
        EditorUtility.SetDirty(cue);
        return cue;
    }

    // 짧은 소리는 메모리에 풀어 두고, 긴 반복음(환경음)은 압축한 채로
    private static void ConfigureImport(string path, bool loop)
    {
        if (AssetImporter.GetAtPath(path) is not AudioImporter importer) return;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        AudioClipLoadType loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        if (settings.loadType == loadType && importer.forceToMono) return;

        settings.loadType = loadType;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.7f;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = true;
        importer.SaveAndReimport();
    }

    // SfxCue 필드 중 비어 있고 이름이 맞는 효과음이 있는 칸을 채움
    private static int LinkFields(Object target, Dictionary<string, SfxCue> cues, System.Func<string, string> cueNameOf)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.GetIterator();
        int linked = 0;
        while (prop.NextVisible(true))
        {
            if (prop.propertyType != SerializedPropertyType.ObjectReference || prop.objectReferenceValue != null) continue;
            if (prop.type != "PPtr<$SfxCue>") continue;

            string cueName = cueNameOf(prop.name);
            if (cueName == null || !cues.TryGetValue(cueName, out SfxCue cue)) continue;

            prop.objectReferenceValue = cue;
            linked++;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return linked;
    }

    public static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
