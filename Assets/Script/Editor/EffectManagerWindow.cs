using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 이펙트 관리 창 (Tools > Effect > 이펙트 관리)
// - 이펙트 탭: 분류별 목록, 검색, 미리보기(게임과 같은 계산), 임시/일부 교체/교체됨 표시, 쓰이는 곳
//   펼치면 설명·수치 편집, 이미터 줄 위로 스프라이트를 끌어다 놓으면 그 이미터의 그림 교체, 임시 그림으로 되돌리기
// - 스프라이트 탭: 이펙트에 쓰인 그림 목록. 줄 위로 스프라이트를 놓으면 그 그림을 쓰는 모든 이펙트에서 교체
// - Custom 폴더 적용 (Assets/Sprites/Effect/Custom에 임시 그림과 같은 이름의 파일을 넣어 한꺼번에 교체)
public class EffectManagerWindow : EditorWindow
{
    private class Entry
    {
        public EffectData data;
        public string category;
        public string usage;
        public Color tint = Color.white;
        public bool expanded;
    }

    // 미리보기 기준 대상 크기 (화면 평면에서 본 너비·높이, m)
    private static readonly (string label, Vector2 size)[] TargetSizes =
    {
        ("점", Vector2.zero),
        ("작은 대상 1×1m", new Vector2(1f, 1f)),
        ("사람 1×1.8m", new Vector2(1f, 1.8f)),
        ("바위 2×1.5m", new Vector2(2f, 1.5f)),
        ("나무 5.8×6m", new Vector2(5.8f, 6f)),
    };

    private static readonly (string label, Color color)[] Backgrounds =
    {
        ("풀", new Color(0.36f, 0.6f, 0.26f)),
        ("모래", new Color(0.86f, 0.76f, 0.56f)),
        ("눈", new Color(0.86f, 0.87f, 0.88f)),
        ("빙판", new Color(0.66f, 0.75f, 0.8f)),
        ("물", new Color(0.2f, 0.42f, 0.72f)),
        ("밤", new Color(0.08f, 0.09f, 0.14f)),
    };

    // 게임 화면 기준 1m의 픽셀 수 (720p, 직교 크기 9.1)
    private const float GamePixelsPerMeter = 40f;
    private const float PreviewHeight = 230f;

    private static readonly Color PlaceholderColor = new(1f, 0.75f, 0.3f);
    private static readonly Color PartialColor = new(0.85f, 0.85f, 0.4f);
    private static readonly Color ReplacedColor = new(0.45f, 0.85f, 0.45f);

    private readonly List<Entry> entries = new();
    private readonly HashSet<string> collapsed = new();
    private string search = string.Empty;
    private bool onlyPlaceholder;
    private int tab;
    private Vector2 scroll;
    private readonly Dictionary<EffectData, Editor> editors = new();

    // 미리보기
    private Entry previewEntry;
    private readonly List<EffectParticle> previewParticles = new();
    private int targetSize = 2;
    private int background;
    private int zoom = 2;
    private bool loop = true;
    // 0이면 한 번 뿌리고 다 끝나면 RestTime 쉬었다 다시, 0보다 크면 이 간격으로 계속 뿌림 (불씨·연기처럼 반복되는 이펙트)
    private float repeatInterval;
    private float repeatTimer;
    private const float RestTime = 0.4f;
    private double lastTime;

    [MenuItem("Tools/Effect/이펙트 관리")]
    public static void Open()
    {
        var window = GetWindow<EffectManagerWindow>("이펙트 관리");
        window.minSize = new Vector2(560f, 400f);
        window.Refresh();
    }

    private void OnEnable()
    {
        Refresh();
        Undo.undoRedoPerformed += Repaint;
        EditorApplication.update += Tick;
        lastTime = EditorApplication.timeSinceStartup;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= Repaint;
        EditorApplication.update -= Tick;
        foreach (Editor editor in editors.Values) if (editor != null) DestroyImmediate(editor);
        editors.Clear();
    }

    private void OnFocus() => Refresh();

    #region 목록

    private void Refresh()
    {
        Dictionary<EffectData, (List<string> users, Color? tint)> usage = CollectUsage();
        var expandedNames = new HashSet<string>(entries.Where(e => e.expanded && e.data != null).Select(e => e.data.name));
        string previewName = previewEntry?.data != null ? previewEntry.data.name : null;

        entries.Clear();
        foreach (EffectData data in EffectSpriteBuilder.FindAll())
        {
            string path = AssetDatabase.GetAssetPath(data);
            string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path));
            usage.TryGetValue(data, out var use);
            entries.Add(new Entry
            {
                data = data,
                category = folder == "Effect" ? "기타" : folder,
                usage = use.users != null ? string.Join(", ", use.users) : "쓰이는 곳 없음",
                tint = use.tint ?? Color.white,
                expanded = expandedNames.Contains(data.name),
            });
        }
        entries.Sort((a, b) => a.data.name.CompareTo(b.data.name));
        previewEntry = entries.FirstOrDefault(e => e.data.name == previewName);
        Repaint();
    }

    // 이펙트 → 쓰는 곳 이름들과 미리보기 색 (자원의 effectColor처럼 재생할 때 곱하는 색이 있으면 첫 번째 것)
    private static Dictionary<EffectData, (List<string> users, Color? tint)> CollectUsage()
    {
        var usage = new Dictionary<EffectData, (List<string> users, Color? tint)>();
        void Add(EffectData data, string label, Color? tint = null)
        {
            if (!usage.TryGetValue(data, out var entry)) entry = (new List<string>(), null);
            if (!entry.users.Contains(label)) entry.users.Add(label);
            if (entry.tint == null && tint != null) entry.tint = tint;
            usage[data] = entry;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Data", "Assets/Resources" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null || asset is EffectData) continue;

            SerializedProperty prop = new SerializedObject(asset).GetIterator();
            while (prop.NextVisible(true))
            {
                if (prop.propertyType != SerializedPropertyType.ObjectReference || prop.objectReferenceValue is not EffectData data) continue;
                Add(data, $"{asset.name}.{prop.displayName}", asset is PropData propData ? propData.effectColor : (Color?)null);
            }
        }

        foreach (string folder in new[] { "Assets/Prefabs", "Assets/Scenes" })
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab t:Scene", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string kind = path.EndsWith(".unity") ? "씬" : "프리팹";
                foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                {
                    if (AssetDatabase.GetMainAssetTypeAtPath(dependency) != typeof(EffectData)) continue;
                    Add(AssetDatabase.LoadAssetAtPath<EffectData>(dependency), $"{kind} {System.IO.Path.GetFileNameWithoutExtension(path)}");
                }
            }
        }
        return usage;
    }

    private void OnGUI()
    {
        DrawToolbar();
        tab = GUILayout.Toolbar(tab, new[] { "이펙트", "스프라이트" });
        if (previewEntry != null) DrawPreview();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (tab == 0) DrawEffects();
        else DrawSprites();
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120f));
            onlyPlaceholder = GUILayout.Toggle(onlyPlaceholder, "임시만", EditorStyles.toolbarButton, GUILayout.Width(52f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Custom 폴더 적용", EditorStyles.toolbarButton))
            {
                EffectSpriteBuilder.ApplyCustomFolder();
                Refresh();
            }
            if (GUILayout.Button("Custom 폴더 열기", EditorStyles.toolbarButton))
            {
                EffectSpriteBuilder.EnsureFolder(EffectSpriteBuilder.CustomRoot);
                EditorUtility.RevealInFinder(EffectSpriteBuilder.CustomRoot);
            }
            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton)) Refresh();
        }
        EditorGUILayout.HelpBox("이펙트 탭: 이미터 줄 위로 스프라이트를 끌어다 놓으면 그 이미터의 그림만 바뀝니다 (여러 장이면 프레임으로 들어감). "
            + "스프라이트 탭: 줄 위로 놓으면 그 그림을 쓰는 모든 이펙트에서 바뀝니다. "
            + "Custom 폴더에 임시 그림과 같은 이름의 파일(예: Chip_0.png)을 넣고 'Custom 폴더 적용'을 누르면 한꺼번에 바뀝니다. 새 그림은 PPU 10(1픽셀 = 0.1m)으로 들어갑니다.", MessageType.None);
    }

    private void DrawEffects()
    {
        foreach (var group in entries.Where(Matches).GroupBy(e => e.category).OrderBy(g => CategoryOrder(g.Key)))
        {
            bool open = !collapsed.Contains(group.Key);
            int replaced = group.Count(e => EffectSpriteBuilder.ReplaceState(e.data) == 2);
            string title = $"{EffectDescriptions.CategoryLabel(group.Key)}  ({group.Count()}개, 교체 {replaced})";
            bool nowOpen = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);
            if (nowOpen != open)
            {
                if (nowOpen) collapsed.Remove(group.Key);
                else collapsed.Add(group.Key);
            }
            if (!nowOpen) continue;

            foreach (Entry entry in group) DrawEntry(entry);
            EditorGUILayout.Space(4f);
        }
    }

    private bool Matches(Entry entry)
    {
        if (entry.data == null) return false;
        if (onlyPlaceholder && EffectSpriteBuilder.ReplaceState(entry.data) == 2) return false;
        if (string.IsNullOrWhiteSpace(search)) return true;

        string query = search.Trim();
        return entry.data.name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0
            || (entry.data.description ?? string.Empty).Contains(query)
            || entry.usage.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int CategoryOrder(string category)
    {
        int index = EffectDescriptions.Categories.Keys.ToList().IndexOf(category);
        return index < 0 ? 999 : index;
    }

    private void DrawEntry(Entry entry)
    {
        EffectData data = entry.data;
        int state = EffectSpriteBuilder.ReplaceState(data);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶", GUILayout.Width(26f))) StartPreview(entry);

                entry.expanded = EditorGUILayout.Foldout(entry.expanded, data.name, true);

                Color old = GUI.color;
                GUI.color = state == 0 ? PlaceholderColor : state == 1 ? PartialColor : ReplacedColor;
                GUILayout.Label(state == 0 ? "임시" : state == 1 ? "일부 교체" : "교체됨", EditorStyles.miniButton, GUILayout.Width(60f));
                GUI.color = old;

                GUILayout.Label(data.description, EditorStyles.label, GUILayout.MinWidth(80f));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"이미터 {(data.emitters != null ? data.emitters.Length : 0)}", EditorStyles.miniLabel, GUILayout.Width(52f));
            }
            EditorGUILayout.LabelField(entry.usage, EditorStyles.miniLabel);

            if (entry.expanded) DrawDetails(entry);
        }
    }

    private void DrawDetails(Entry entry)
    {
        EffectData data = entry.data;

        EditorGUI.BeginChangeCheck();
        string description = EditorGUILayout.TextField("설명", data.description);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(data, "이펙트 설명 수정");
            data.description = description;
            EditorUtility.SetDirty(data);
        }

        // 이미터별 그림: 끌어다 놓아 교체, 되돌리기
        if (data.emitters != null)
        {
            for (int i = 0; i < data.emitters.Length; i++) DrawEmitterSprites(data, data.emitters[i], i);
        }

        // 수치는 기본 인스펙터 그대로 (바꾸면 미리보기가 다음 재생부터 반영)
        if (!editors.TryGetValue(data, out Editor editor) || editor == null)
        {
            editor = Editor.CreateEditor(data);
            editors[data] = editor;
        }
        EditorGUI.indentLevel++;
        editor.OnInspectorGUI();
        EditorGUI.indentLevel--;

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("에셋 선택", GUILayout.Width(80f))) Selection.activeObject = data;
        }
    }

    private void DrawEmitterSprites(EffectData data, ParticleEmitter emitter, int index)
    {
        bool placeholder = EffectSpriteBuilder.IsPlaceholder(emitter);
        Rect rect = EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label($"이미터 {index + 1}", GUILayout.Width(56f));

        Color old = GUI.color;
        GUI.color = placeholder ? PlaceholderColor : ReplacedColor;
        GUILayout.Label(placeholder ? "임시" : "교체됨", EditorStyles.miniButton, GUILayout.Width(50f));
        GUI.color = old;

        if (emitter.frames != null)
        {
            foreach (Sprite sprite in emitter.frames) DrawThumbnail(sprite, 28f);
        }
        GUILayout.FlexibleSpace();
        using (new EditorGUI.DisabledScope(!EffectSpriteBuilder.CanRevert(emitter)))
        {
            if (GUILayout.Button("임시로 되돌리기", GUILayout.Width(100f)))
            {
                EffectSpriteBuilder.Revert(data, emitter);
                AssetDatabase.SaveAssets();
                GUIUtility.ExitGUI();
            }
        }
        EditorGUILayout.EndHorizontal();

        Sprite[] dropped = AcceptSprites(rect);
        if (dropped != null)
        {
            EffectSpriteBuilder.SetFrames(data, emitter, dropped, "이펙트 그림 교체");
            AssetDatabase.SaveAssets();
            StartPreview(entries.FirstOrDefault(e => e.data == data));
        }
    }

    #endregion

    #region 스프라이트 탭

    private void DrawSprites()
    {
        // 그림 → 쓰는 이펙트 이름들
        var users = new Dictionary<Sprite, List<string>>();
        foreach (Entry entry in entries)
        {
            if (entry.data?.emitters == null) continue;
            foreach (ParticleEmitter emitter in entry.data.emitters)
            {
                foreach (Sprite sprite in emitter.frames ?? new Sprite[0])
                {
                    if (sprite == null) continue;
                    if (!users.TryGetValue(sprite, out List<string> list)) users[sprite] = list = new List<string>();
                    if (!list.Contains(entry.data.name)) list.Add(entry.data.name);
                }
            }
        }

        foreach (var pair in users.OrderBy(p => !EffectSpriteBuilder.IsPlaceholder(p.Key)).ThenBy(p => p.Key.name))
        {
            Sprite sprite = pair.Key;
            bool placeholder = EffectSpriteBuilder.IsPlaceholder(sprite);
            if (onlyPlaceholder && !placeholder) continue;
            if (!string.IsNullOrWhiteSpace(search)
                && sprite.name.IndexOf(search.Trim(), System.StringComparison.OrdinalIgnoreCase) < 0
                && !pair.Value.Any(n => n.IndexOf(search.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0)) continue;

            Rect rect = EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            DrawThumbnail(sprite, 40f);
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(sprite.name, EditorStyles.boldLabel);
                    Color old = GUI.color;
                    GUI.color = placeholder ? PlaceholderColor : ReplacedColor;
                    GUILayout.Label(placeholder ? "임시" : "교체됨", EditorStyles.miniButton, GUILayout.Width(50f));
                    GUI.color = old;
                    GUILayout.Label($"{sprite.rect.width:0}×{sprite.rect.height:0}px, PPU {sprite.pixelsPerUnit:0}", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("선택", GUILayout.Width(44f))) Selection.activeObject = sprite;
                }
                GUILayout.Label($"이펙트 {pair.Value.Count}개: {string.Join(", ", pair.Value)}", EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndHorizontal();

            Sprite[] dropped = AcceptSprites(rect);
            if (dropped != null && dropped[0] != sprite)
            {
                int count = EffectSpriteBuilder.ReplaceEverywhere(sprite, dropped[0], "이펙트 그림 일괄 교체");
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent($"{sprite.name} → {dropped[0].name} ({count}칸)"));
                GUIUtility.ExitGUI();
            }
        }
    }

    #endregion

    #region 공용 그리기·끌어다 놓기

    private static void DrawThumbnail(Sprite sprite, float size)
    {
        Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.25f));
        if (sprite == null) return;

        Rect tr = sprite.textureRect;
        float scale = Mathf.Min(rect.width / tr.width, rect.height / tr.height);
        // 작은 픽셀 그림은 정수 배율로 키워 흐려지지 않게
        if (scale >= 1f) scale = Mathf.Floor(scale);
        Vector2 drawSize = tr.size * scale;
        var drawRect = new Rect(rect.center - drawSize * 0.5f, drawSize);
        DrawSprite(drawRect, sprite, Color.white);
    }

    private static void DrawSprite(Rect rect, Sprite sprite, Color color)
    {
        Texture2D texture = sprite.texture;
        Rect tr = sprite.textureRect;
        var uv = new Rect(tr.x / texture.width, tr.y / texture.height, tr.width / texture.width, tr.height / texture.height);
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
        GUI.color = old;
    }

    // 이 칸에 스프라이트(또는 스프라이트로 들여온 텍스처)를 놓았으면 그 목록, 아니면 null
    private static Sprite[] AcceptSprites(Rect rect)
    {
        Event e = Event.current;
        if (!rect.Contains(e.mousePosition)) return null;
        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return null;

        var sprites = new List<Sprite>();
        foreach (Object obj in DragAndDrop.objectReferences)
        {
            if (obj is Sprite sprite) sprites.Add(sprite);
            else if (obj is Texture2D) sprites.AddRange(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(obj)).OfType<Sprite>());
        }
        if (sprites.Count == 0) return null;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (e.type == EventType.DragUpdated)
        {
            e.Use();
            return null;
        }

        DragAndDrop.AcceptDrag();
        e.Use();
        return sprites.OrderBy(s => s.name, new NaturalOrder()).ToArray();
    }

    // Puff_2가 Puff_10보다 앞에 오도록 숫자를 숫자로 비교
    private class NaturalOrder : IComparer<string>
    {
        public int Compare(string a, string b) => EditorUtility.NaturalCompare(a, b);
    }

    #endregion

    #region 미리보기

    private void StartPreview(Entry entry)
    {
        if (entry == null) return;
        previewEntry = entry;
        previewParticles.Clear();
        string name = entry.data.name;
        // 게임에서 LoopingEffect·불타는 이펙트로 계속 뿌리는 것은 반복 간격을 기본으로 둠
        repeatInterval = name.Contains("Embers") ? 0.35f : name == "SmelterSmoke" ? 0.45f : 0f;
        repeatTimer = repeatInterval > 0f ? repeatInterval : RestTime;
        Emit();
    }

    private void Emit()
    {
        EffectData data = previewEntry?.data;
        if (data?.emitters == null) return;

        Vector2 size = TargetSizes[targetSize].size;
        foreach (ParticleEmitter emitter in data.emitters)
        {
            if (emitter.frames == null || emitter.frames.Length == 0) continue;
            int count = EffectParticle.RollCount(emitter, 1f);
            for (int i = 0; i < count; i++)
            {
                var p = new EffectParticle();
                p.Init(emitter, size.x, size.y, previewEntry.tint);
                previewParticles.Add(p);
            }
        }
    }

    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Min((float)(now - lastTime), 0.05f);
        lastTime = now;
        if (previewEntry == null) return;

        for (int i = previewParticles.Count - 1; i >= 0; i--)
        {
            previewParticles[i].Tick(dt);
            if (previewParticles[i].Finished) previewParticles.RemoveAt(i);
        }

        if (loop && (repeatInterval > 0f || previewParticles.Count == 0))
        {
            repeatTimer -= dt;
            if (repeatTimer <= 0f)
            {
                repeatTimer = repeatInterval > 0f ? repeatInterval * Random.Range(0.7f, 1.3f) : RestTime;
                Emit();
            }
        }
        Repaint();
    }

    private void DrawPreview()
    {
        EffectData data = previewEntry.data;
        if (data == null)
        {
            previewEntry = null;
            return;
        }

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label($"미리보기: {data.name}", EditorStyles.boldLabel, GUILayout.MinWidth(120f));
            GUILayout.FlexibleSpace();
            targetSize = EditorGUILayout.Popup(targetSize, TargetSizes.Select(t => t.label).ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(110f));
            background = EditorGUILayout.Popup(background, Backgrounds.Select(b => b.label).ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(52f));
            zoom = EditorGUILayout.Popup(zoom - 1, new[] { "×1", "×2", "×3" }, EditorStyles.toolbarPopup, GUILayout.Width(40f)) + 1;
            loop = GUILayout.Toggle(loop, "반복", EditorStyles.toolbarButton, GUILayout.Width(40f));
            if (GUILayout.Button("다시", EditorStyles.toolbarButton, GUILayout.Width(40f))) StartPreview(previewEntry);
            if (GUILayout.Button("닫기", EditorStyles.toolbarButton, GUILayout.Width(40f)))
            {
                previewEntry = null;
                previewParticles.Clear();
                GUIUtility.ExitGUI();
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            previewEntry.tint = EditorGUILayout.ColorField(new GUIContent("곱할 색", "자원의 effectColor처럼 재생할 때 곱하는 색 (쓰는 곳에서 가져옴)"), previewEntry.tint, GUILayout.Width(220f));
            repeatInterval = EditorGUILayout.FloatField(new GUIContent("반복 간격", "0이면 한 번 뿌리고 끝날 때마다 다시, 0보다 크면 이 간격으로 계속 (불씨·연기)"), repeatInterval, GUILayout.Width(220f));
            repeatInterval = Mathf.Max(0f, repeatInterval);
        }

        Rect area = GUILayoutUtility.GetRect(10f, PreviewHeight, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area, Backgrounds[background].color);
        GUI.BeginClip(area);
        var local = new Rect(0f, 0f, area.width, area.height);
        float ppm = GamePixelsPerMeter * zoom;
        // 바닥선과 원점 (가운데 아래쪽)
        var origin = new Vector2(local.width * 0.5f, local.height - 24f);
        EditorGUI.DrawRect(new Rect(0f, origin.y, local.width, 1f), new Color(0f, 0f, 0f, 0.35f));

        // 기준 대상 범위
        Vector2 size = TargetSizes[targetSize].size * ppm;
        if (size.x > 0f) DrawOutline(new Rect(origin.x - size.x * 0.5f, origin.y - size.y, size.x, size.y), new Color(1f, 1f, 1f, 0.35f));
        else EditorGUI.DrawRect(new Rect(origin.x - 2f, origin.y - 2f, 4f, 4f), new Color(1f, 1f, 1f, 0.6f));

        foreach (EffectParticle p in previewParticles)
        {
            if (p.Waiting) continue;
            Sprite sprite = p.CurrentSprite;
            if (sprite == null) continue;

            Vector2 pos = p.DrawPosition;
            float scale = p.DrawScale;
            Vector2 drawSize = sprite.rect.size / sprite.pixelsPerUnit * scale * ppm;
            Vector2 pivot = sprite.pivot / sprite.rect.size;
            var center = new Vector2(origin.x + pos.x * ppm, origin.y - pos.y * ppm);
            var rect = new Rect(center.x - drawSize.x * pivot.x, center.y - drawSize.y * (1f - pivot.y), drawSize.x, drawSize.y);
            DrawSprite(rect, sprite, p.DrawColor);
        }

        GUI.Label(new Rect(6f, local.height - 20f, local.width - 12f, 18f),
            $"1m = {ppm:0}px (게임 화면 ×{zoom})  ·  파티클 {previewParticles.Count}개", EditorStyles.whiteMiniLabel);
        GUI.EndClip();
    }

    private static void DrawOutline(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
    }

    #endregion
}
