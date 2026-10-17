using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 효과음 관리 창 (Tools > Sfx > 효과음 관리)
// - 분류별 목록, 검색, 미리 듣기, 임시음/교체됨 표시, 쓰이는 곳
// - 줄 위로 오디오 파일을 끌어다 놓으면 그 효과음의 클립을 교체 (여러 개면 변형으로)
// - 볼륨·피치 범위·최소 간격 조절, 임시음으로 되돌리기
// - Custom 폴더 적용 (Assets/Audio/Sfx/Custom에 효과음 이름과 같은 파일을 넣어 한꺼번에 교체)
public class SfxManagerWindow : EditorWindow
{
    private class Entry
    {
        public SfxCue cue;
        public string category;
        public string usage;
        public bool expanded;
    }

    private readonly List<Entry> entries = new();
    private readonly HashSet<string> collapsed = new();
    private string search = string.Empty;
    private bool onlyPlaceholder;
    private Vector2 scroll;
    private AudioSource previewSource;

    private static readonly Color PlaceholderColor = new(1f, 0.75f, 0.3f);
    private static readonly Color ReplacedColor = new(0.45f, 0.85f, 0.45f);

    [MenuItem("Tools/Sfx/효과음 관리")]
    public static void Open()
    {
        var window = GetWindow<SfxManagerWindow>("효과음 관리");
        window.minSize = new Vector2(560f, 300f);
        window.Refresh();
    }

    private void OnEnable()
    {
        Refresh();
        Undo.undoRedoPerformed += Repaint;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= Repaint;
        if (previewSource != null) DestroyImmediate(previewSource.gameObject);
    }

    private void OnFocus() => Refresh();

    // 효과음 목록과 쓰이는 곳을 다시 모음
    private void Refresh()
    {
        Dictionary<string, List<string>> usage = CollectUsage();
        var expandedNames = new HashSet<string>(entries.Where(e => e.expanded && e.cue != null).Select(e => e.cue.name));

        entries.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:SfxCue", new[] { SfxCueBuilder.CueRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var cue = AssetDatabase.LoadAssetAtPath<SfxCue>(path);
            if (cue == null) continue;

            string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path));
            entries.Add(new Entry
            {
                cue = cue,
                category = folder,
                usage = usage.TryGetValue(path, out List<string> users) ? string.Join(", ", users) : "쓰이는 곳 없음",
                expanded = expandedNames.Contains(cue.name),
            });
        }
        entries.Sort((a, b) => a.cue.name.CompareTo(b.cue.name));
        Repaint();
    }

    // 효과음 에셋 경로 → 그 효과음을 쓰는 데이터·프리팹 이름 (필드 이름까지)
    private static Dictionary<string, List<string>> CollectUsage()
    {
        var usage = new Dictionary<string, List<string>>();
        void Add(Object cue, string label)
        {
            string path = AssetDatabase.GetAssetPath(cue);
            if (!usage.TryGetValue(path, out List<string> list)) usage[path] = list = new List<string>();
            if (!list.Contains(label)) list.Add(label);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Data", "Assets/Resources" }))
        {
            Object asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null || asset is SfxCue) continue;

            SerializedProperty prop = new SerializedObject(asset).GetIterator();
            while (prop.NextVisible(true))
            {
                if (prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue is SfxCue cue)
                {
                    Add(cue, asset is SfxSetting ? "공용 설정" : $"{asset.name}.{prop.displayName}");
                }
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (string dependency in AssetDatabase.GetDependencies(path, false))
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(dependency) != typeof(SfxCue)) continue;
                Add(AssetDatabase.LoadAssetAtPath<SfxCue>(dependency), $"프리팹 {System.IO.Path.GetFileNameWithoutExtension(path)}");
            }
        }
        return usage;
    }

    private void OnGUI()
    {
        DrawToolbar();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var group in entries.Where(Matches).GroupBy(e => e.category).OrderBy(g => CategoryOrder(g.Key)))
        {
            bool open = !collapsed.Contains(group.Key);
            int replaced = group.Count(e => !SfxCueBuilder.IsPlaceholder(e.cue));
            string title = $"{SfxDescriptions.CategoryLabel(group.Key)}  ({group.Count()}개, 교체 {replaced})";
            bool nowOpen = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
            if (nowOpen != open)
            {
                if (nowOpen) collapsed.Remove(group.Key);
                else collapsed.Add(group.Key);
            }
            if (!nowOpen) continue;

            foreach (Entry entry in group)
            {
                DrawEntry(entry);
            }
            EditorGUILayout.Space(4f);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120f));
            onlyPlaceholder = GUILayout.Toggle(onlyPlaceholder, "임시음만", EditorStyles.toolbarButton, GUILayout.Width(64f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("■ 정지", EditorStyles.toolbarButton, GUILayout.Width(52f))) StopPreview();
            if (GUILayout.Button("Custom 폴더 적용", EditorStyles.toolbarButton))
            {
                SfxCueBuilder.ApplyCustomFolder();
                Refresh();
            }
            if (GUILayout.Button("Custom 폴더 열기", EditorStyles.toolbarButton))
            {
                SfxCueBuilder.EnsureFolder(SfxCueBuilder.CustomRoot);
                EditorUtility.RevealInFinder(SfxCueBuilder.CustomRoot);
            }
            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton)) Refresh();
        }
        EditorGUILayout.HelpBox("줄 위로 오디오 파일을 끌어다 놓으면 교체됩니다 (여러 개면 변형으로 들어감). "
            + "Custom 폴더에 효과음 이름과 같은 파일(예: hitWood.wav, hitWood_2.wav)을 넣고 'Custom 폴더 적용'을 누르면 한꺼번에 교체됩니다.", MessageType.None);
    }

    private bool Matches(Entry entry)
    {
        if (entry.cue == null) return false;
        if (onlyPlaceholder && !SfxCueBuilder.IsPlaceholder(entry.cue)) return false;
        if (string.IsNullOrWhiteSpace(search)) return true;

        string query = search.Trim();
        return entry.cue.name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0
            || (entry.cue.description ?? string.Empty).Contains(query)
            || entry.usage.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int CategoryOrder(string category)
    {
        int index = SfxDescriptions.Categories.Keys.ToList().IndexOf(category);
        return index < 0 ? 999 : index;
    }

    private void DrawEntry(Entry entry)
    {
        SfxCue cue = entry.cue;
        bool placeholder = SfxCueBuilder.IsPlaceholder(cue);

        Rect rowRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(cue.IsEmpty))
            {
                if (GUILayout.Button("▶", GUILayout.Width(26f))) Preview(cue);
            }

            entry.expanded = EditorGUILayout.Foldout(entry.expanded, cue.name, true);

            Color old = GUI.color;
            GUI.color = placeholder ? PlaceholderColor : ReplacedColor;
            GUILayout.Label(placeholder ? "임시음" : "교체됨", EditorStyles.miniButton, GUILayout.Width(50f));
            GUI.color = old;

            GUILayout.Label(cue.description, EditorStyles.label, GUILayout.MinWidth(80f));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"클립 {(cue.clips != null ? cue.clips.Length : 0)}", EditorStyles.miniLabel, GUILayout.Width(44f));
        }
        EditorGUILayout.LabelField(entry.usage, EditorStyles.miniLabel);

        if (entry.expanded) DrawDetails(cue, placeholder);
        EditorGUILayout.EndVertical();

        HandleDrop(rowRect, cue);
    }

    private void DrawDetails(SfxCue cue, bool placeholder)
    {
        EditorGUI.BeginChangeCheck();
        string description = EditorGUILayout.TextField("설명", cue.description);
        float volume = EditorGUILayout.Slider("볼륨", cue.volume, 0f, 1f);
        Vector2 pitch = cue.pitchRange;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("피치 범위");
            EditorGUILayout.MinMaxSlider(ref pitch.x, ref pitch.y, 0.5f, 2f);
            pitch.x = EditorGUILayout.FloatField(pitch.x, GUILayout.Width(40f));
            pitch.y = EditorGUILayout.FloatField(pitch.y, GUILayout.Width(40f));
        }
        float interval = EditorGUILayout.FloatField(new GUIContent("최소 간격 (초)", "이 시간 안에 다시 재생하면 건너뜀"), cue.minInterval);
        int voices = EditorGUILayout.IntSlider(new GUIContent("동시 재생 수", "이 효과음이 동시에 날 수 있는 최대 개수"), cue.maxVoices, 1, 8);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(cue, "효과음 수정");
            cue.description = description;
            cue.volume = volume;
            cue.pitchRange = new Vector2(Mathf.Min(pitch.x, pitch.y), Mathf.Max(pitch.x, pitch.y));
            cue.minInterval = Mathf.Max(0f, interval);
            cue.maxVoices = voices;
            EditorUtility.SetDirty(cue);
        }

        // 클립 목록: 하나씩 듣기, 빼기
        if (cue.clips != null)
        {
            for (int i = 0; i < cue.clips.Length; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    AudioClip clip = cue.clips[i];
                    using (new EditorGUI.DisabledScope(clip == null))
                    {
                        if (GUILayout.Button("▶", GUILayout.Width(26f))) PlayClip(clip, cue.volume, 1f);
                    }
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField(clip, typeof(AudioClip), false);
                    }
                    if (clip != null) GUILayout.Label($"{clip.length:0.00}초", EditorStyles.miniLabel, GUILayout.Width(44f));
                    if (GUILayout.Button("−", GUILayout.Width(22f)))
                    {
                        var list = cue.clips.ToList();
                        list.RemoveAt(i);
                        SfxCueBuilder.SetClips(cue, list.ToArray(), "효과음 클립 빼기");
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("에셋 선택", GUILayout.Width(80f))) Selection.activeObject = cue;
            using (new EditorGUI.DisabledScope(placeholder))
            {
                if (GUILayout.Button("임시음으로 되돌리기", GUILayout.Width(130f)))
                {
                    if (!SfxCueBuilder.RevertToPlaceholder(cue)) EditorUtility.DisplayDialog("되돌리기", $"'{cue.name}'의 임시음이 없습니다.", "확인");
                    GUIUtility.ExitGUI();
                }
            }
        }
    }

    // 줄 위에 오디오 파일을 놓으면 클립 교체 (Ctrl을 누르고 놓으면 기존 클립 뒤에 변형으로 추가)
    private void HandleDrop(Rect rect, SfxCue cue)
    {
        Event e = Event.current;
        if (!rect.Contains(e.mousePosition)) return;
        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;

        AudioClip[] dropped = DragAndDrop.objectReferences.OfType<AudioClip>().ToArray();
        if (dropped.Length == 0) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            bool append = e.control || e.command;
            AudioClip[] clips = append && cue.clips != null ? cue.clips.Concat(dropped).ToArray() : dropped;
            SfxCueBuilder.SetClips(cue, clips, "효과음 교체");
            AssetDatabase.SaveAssets();
            Preview(cue);
        }
        e.Use();
    }

    // 게임에서처럼 클립 하나를 고르고 피치를 흔들어 재생 (간격 제한은 무시)
    private void Preview(SfxCue cue)
    {
        if (cue.IsEmpty) return;
        AudioClip clip = cue.clips[Random.Range(0, cue.clips.Length)];
        PlayClip(clip, cue.volume, Random.Range(cue.pitchRange.x, cue.pitchRange.y));
    }

    private void PlayClip(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;
        if (previewSource == null)
        {
            var host = EditorUtility.CreateGameObjectWithHideFlags("SfxPreview", HideFlags.HideAndDontSave, typeof(AudioSource));
            previewSource = host.GetComponent<AudioSource>();
            previewSource.playOnAwake = false;
            previewSource.spatialBlend = 0f;
        }
        previewSource.Stop();
        previewSource.clip = clip;
        previewSource.volume = volume;
        previewSource.pitch = pitch;
        previewSource.loop = false;
        previewSource.Play();
    }

    private void StopPreview()
    {
        if (previewSource != null) previewSource.Stop();
    }
}
