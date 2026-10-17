using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 이펙트 개발 도구의 에셋 작업 (이펙트 관리 창과 Tools > Effect 메뉴가 사용)
// - 임시 스프라이트: 아래 Patterns의 글자 그림으로 PNG를 만듦 (#: 흰색, +: 밝은 회색, .: 반투명, 공백: 투명)
//   흰색으로 그리고 색은 EffectData에서 입힘. 그림 1픽셀 = 0.1m (PPU 10, 자원 그림과 같은 밀도)
// - 분류 정리: Assets/Data/Effect 아래 분류 폴더로 옮기고 빈 설명을 채우고, 임시 그림을 기록
// - Custom 폴더: 임시 스프라이트와 이름이 같은 그림(예: Chip_0.png)을 넣고 적용하면 그 그림을 쓰는 모든 이펙트에서 교체
public static class EffectSpriteBuilder
{
    public const string EffectRoot = "Assets/Data/Effect";
    public const string PlaceholderRoot = "Assets/Sprites/Placeholder/Particle";
    public const string CustomRoot = "Assets/Sprites/Effect/Custom";
    // 이 폴더 아래 그림은 모두 임시 그림으로 봄 (물방울·김 등 다른 임시 그림 포함)
    private const string AnyPlaceholderRoot = "Assets/Sprites/Placeholder/";
    public const float PixelsPerUnit = 10f;

    public static readonly Dictionary<string, string[]> Patterns = new()
    {
        ["Chip_0"] = new[] { "##", "#+" },
        ["Chip_1"] = new[] { "##+", "###", "+# " },
        ["Chip_2"] = new[] { " # ", "###" },
        ["Ember"] = new[] { "##", "##" },
        ["Spark"] = new[] { " # ", "###", " # " },
        ["Leaf"] = new[] { " ##", "###", "#+ " },
        ["Spike"] = new[] { "#", "#", "+" },
        ["Puff_0"] = new[] { "  ##  ", " #### ", "######", "######", " #### ", "  ##  " },
        ["Puff_1"] = new[] { "  ####  ", " ###### ", "########", "####+###", "###++###", "########", " ###### ", "  ####  " },
        ["Puff_2"] = new[] { "  ####  ", " ##..## ", "##....##", "#......#", "#......#", "##....##", " ##..## ", "  ####  " },
        ["Puff_3"] = new[] { "  .  .  ", "        ", ".      .", "        ", "        ", ".      .", "        ", "  .  .  " },
        ["HitStar_0"] = new[] { "    #    ", "    #    ", "  # # #  ", "   ###   ", "#########", "   ###   ", "  # # #  ", "    #    ", "    #    " },
        ["HitStar_1"] = new[] { "#   #   #", " #  #  # ", "  #   #  ", "         ", "##     ##", "         ", "  #   #  ", " #  #  # ", "#   #   #" },
        ["HitStar_2"] = new[] { "#       #", "         ", "         ", "         ", "         ", "         ", "         ", "         ", "#       #" },
    };

    [MenuItem("Tools/Effect/임시 스프라이트 다시 만들기")]
    public static void BuildPlaceholderSprites()
    {
        EnsureFolder(PlaceholderRoot);
        var paths = new List<string>();
        foreach (var pair in Patterns)
        {
            string path = $"{PlaceholderRoot}/{pair.Key}.png";
            File.WriteAllBytes(path, Draw(pair.Value).EncodeToPNG());
            paths.Add(path);
        }
        AssetDatabase.Refresh();
        foreach (string path in paths) ConfigureImport(path, true);
        Debug.Log($"[EffectSpriteBuilder] 임시 스프라이트 {paths.Count}개를 다시 만들었습니다 ({PlaceholderRoot})");
    }

    [MenuItem("Tools/Effect/이펙트 분류 정리")]
    public static void Organize()
    {
        int moved = 0;
        foreach (EffectData data in FindAll())
        {
            string category = EffectDescriptions.CategoryOf(data.name);
            string path = AssetDatabase.GetAssetPath(data);
            if (category != null)
            {
                string folder = $"{EffectRoot}/{category}";
                string target = $"{folder}/{data.name}.asset";
                if (path != target)
                {
                    EnsureFolder(folder);
                    string error = AssetDatabase.MoveAsset(path, target);
                    if (string.IsNullOrEmpty(error)) moved++;
                    else Debug.LogWarning($"[EffectSpriteBuilder] {data.name} 이동 실패: {error}");
                }
            }

            if (string.IsNullOrEmpty(data.description)) data.description = EffectDescriptions.Describe(data.name);
            RecordPlaceholders(data);
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[EffectSpriteBuilder] 이펙트 분류 정리: {moved}개 이동");
    }

    // Custom 폴더의 그림을 이름이 같은 임시 그림 자리에 넣음. 이름이 맞지 않는 그림은 목록으로 알려 줌
    [MenuItem("Tools/Effect/Custom 폴더 적용")]
    public static void ApplyCustomFolder()
    {
        EnsureFolder(CustomRoot);
        var custom = new Dictionary<string, Sprite>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CustomRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ConfigureImport(path, false);
            foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()) custom[sprite.name] = sprite;
        }

        var placeholders = new HashSet<string>(AllPlaceholderSprites().Select(s => s.name));
        int replaced = 0;
        var unmatched = new List<string>();
        foreach (var pair in custom)
        {
            if (!placeholders.Contains(pair.Key))
            {
                unmatched.Add(pair.Key);
                continue;
            }
            replaced += ReplaceByPlaceholderName(pair.Key, pair.Value, "Custom 폴더 적용");
        }
        AssetDatabase.SaveAssets();

        string message = $"그림 {custom.Count - unmatched.Count}종을 적용했습니다 (바뀐 칸 {replaced}개).";
        if (unmatched.Count > 0) message += "\n\n이름이 맞는 임시 그림이 없어 건너뜀:\n" + string.Join(", ", unmatched);
        EditorUtility.DisplayDialog("Custom 폴더 적용", message, "확인");
    }

    public static IEnumerable<EffectData> FindAll()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:EffectData", new[] { EffectRoot }))
        {
            var data = AssetDatabase.LoadAssetAtPath<EffectData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null) yield return data;
        }
    }

    public static bool IsPlaceholder(Sprite sprite) => sprite == null || AssetDatabase.GetAssetPath(sprite).StartsWith(AnyPlaceholderRoot);

    public static bool IsPlaceholder(ParticleEmitter emitter) => emitter.frames == null || emitter.frames.All(IsPlaceholder);

    // 모든 이미터가 임시 그림이면 0, 모두 교체됐으면 2, 섞여 있으면 1
    public static int ReplaceState(EffectData data)
    {
        if (data.emitters == null || data.emitters.Length == 0) return 0;
        int placeholder = data.emitters.Count(IsPlaceholder);
        return placeholder == data.emitters.Length ? 0 : placeholder == 0 ? 2 : 1;
    }

    // 지금 그림이 모두 임시 그림인 이미터는 그 그림을 기록해 둠 (되돌리기용)
    public static void RecordPlaceholders(EffectData data)
    {
        if (data.emitters == null) return;
        foreach (ParticleEmitter emitter in data.emitters)
        {
            if (IsPlaceholder(emitter) && emitter.frames != null) emitter.placeholderFrames = (Sprite[])emitter.frames.Clone();
        }
    }

    public static void SetFrames(EffectData data, ParticleEmitter emitter, Sprite[] frames, string undoName)
    {
        if (frames == null || frames.Length == 0 || frames.Any(f => f == null)) return;
        Undo.RecordObject(data, undoName);
        if (IsPlaceholder(emitter) && emitter.frames != null) emitter.placeholderFrames = (Sprite[])emitter.frames.Clone();
        emitter.frames = frames;
        EditorUtility.SetDirty(data);
    }

    public static bool CanRevert(ParticleEmitter emitter) => !IsPlaceholder(emitter) && emitter.placeholderFrames != null && emitter.placeholderFrames.Length > 0;

    public static void Revert(EffectData data, ParticleEmitter emitter)
    {
        if (!CanRevert(emitter)) return;
        Undo.RecordObject(data, "임시 그림으로 되돌리기");
        emitter.frames = (Sprite[])emitter.placeholderFrames.Clone();
        EditorUtility.SetDirty(data);
    }

    // 이펙트에 쓰인 그림 하나를 모든 이펙트에서 다른 그림으로 바꿈. 바뀐 칸 수를 돌려줌
    public static int ReplaceEverywhere(Sprite from, Sprite to, string undoName)
    {
        // 빈 그림으로 바꾸면 이펙트가 보이지 않게 되므로 하지 않음
        if (from == null || to == null) return 0;
        int count = 0;
        foreach (EffectData data in FindAll())
        {
            if (data.emitters == null) continue;
            foreach (ParticleEmitter emitter in data.emitters)
            {
                if (emitter.frames == null || !emitter.frames.Contains(from)) continue;
                Sprite[] frames = emitter.frames.Select(f => f == from ? to : f).ToArray();
                count += emitter.frames.Count(f => f == from);
                SetFrames(data, emitter, frames, undoName);
            }
        }
        return count;
    }

    // 기록된 임시 그림 중 이 이름인 칸을 새 그림으로 바꿈 (이미 다른 그림으로 바뀐 칸도 같은 자리면 바꿈)
    public static int ReplaceByPlaceholderName(string placeholderName, Sprite to, string undoName)
    {
        if (to == null) return 0;
        int count = 0;
        foreach (EffectData data in FindAll())
        {
            if (data.emitters == null) continue;
            foreach (ParticleEmitter emitter in data.emitters)
            {
                Sprite[] source = emitter.placeholderFrames != null && emitter.placeholderFrames.Length == emitter.frames?.Length
                    ? emitter.placeholderFrames : emitter.frames;
                if (source == null) continue;

                Sprite[] frames = (Sprite[])emitter.frames.Clone();
                bool changed = false;
                for (int i = 0; i < frames.Length; i++)
                {
                    if (source[i] == null || source[i].name != placeholderName || frames[i] == to) continue;
                    frames[i] = to;
                    changed = true;
                    count++;
                }
                if (changed) SetFrames(data, emitter, frames, undoName);
            }
        }
        return count;
    }

    // 이펙트들이 처음에 쓰던 임시 그림 전부 (기록 + 지금 쓰는 임시 그림)
    public static IEnumerable<Sprite> AllPlaceholderSprites()
    {
        var set = new HashSet<Sprite>();
        foreach (EffectData data in FindAll())
        {
            if (data.emitters == null) continue;
            foreach (ParticleEmitter emitter in data.emitters)
            {
                foreach (Sprite s in emitter.placeholderFrames ?? new Sprite[0]) if (s != null) set.Add(s);
                foreach (Sprite s in emitter.frames ?? new Sprite[0]) if (s != null && IsPlaceholder(s)) set.Add(s);
            }
        }
        return set;
    }

    private static Texture2D Draw(string[] rows)
    {
        int h = rows.Length;
        int w = rows.Max(r => r.Length);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            string row = rows[h - 1 - y];
            for (int x = 0; x < w; x++)
            {
                char c = x < row.Length ? row[x] : ' ';
                tex.SetPixel(x, y, c switch
                {
                    '#' => Color.white,
                    '+' => new Color(0.78f, 0.78f, 0.78f, 1f),
                    '.' => new Color(1f, 1f, 1f, 0.55f),
                    _ => Color.clear,
                });
            }
        }
        return tex;
    }

    // 픽셀 그림 설정 (Point, 무압축). force가 아니면 새로 들어온 그림(PPU가 기본값 100)만 PPU를 10으로 맞춤
    private static void ConfigureImport(string path, bool force)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

        bool fresh = importer.textureType != TextureImporterType.Sprite || Mathf.Approximately(importer.spritePixelsPerUnit, 100f);
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
        }
        if (force || fresh) importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    public static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
