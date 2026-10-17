using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 태양곰 임시 그림·애니메이션 생성 (개발용, 다시 실행하면 같은 파일을 덮어씀)
// - 파츠 그림: Sprites/Placeholder/SunBear/SunBear_*.png (파츠마다 한 장, 최종 그림은 같은 파일을 덮어쓰면 GUID가 유지되어 그대로 연결됨)
// - 애니메이션: Animation/Monster/SunBear/ (상태마다 클립 하나, 컨트롤러 이름 SunBear → 상태 이름 SunBear + 모션)
// - 프리팹: Prefabs/Boss/SunBear.prefab의 Root 아래 파츠를 교체하고 피격 반짝임·그림자 크기를 맞춤
// 기본은 이족보행(TorsoStand), 돌진·도약 계열 모션은 사족보행(TorsoQuad)으로 몸통을 바꿔 끼움
// 그림은 왼쪽을 바라봄 (Facing.baseSpriteFace = false), 픽셀 단위 좌표, 팔다리는 위쪽 끝이 피벗
public static class SunBearPlaceholderBuilder
{
    private const string SpriteFolder = "Assets/Sprites/Placeholder/SunBear";
    private const string AnimFolder = "Assets/Animation/Monster/SunBear";
    private const string PrefabPath = "Assets/Prefabs/Boss/SunBear.prefab";
    private const string ControllerName = "SunBear";
    private const float PixelsPerUnit = 256f;

    // 그리는 순서 (뒤 → 앞)
    private static readonly string[] Parts = { "LegBack", "ArmBack", "TorsoStand", "TorsoQuad", "Head", "LegFront", "ArmFront" };
    private static readonly string[] OldParts = { "Head", "Body", "FrontLeftLeg", "FrontRightLeg", "BackLeftLeg", "BackRightLeg", "Tail" };

    [MenuItem("Tools/보스/태양곰 임시 스프라이트 생성")]
    private static void Build()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[태양곰 임시 그림] 플레이 모드에서는 실행할 수 없음");
            return;
        }

        Dictionary<string, Sprite> sprites = BuildSprites();
        AnimatorController controller = BuildAnimations();
        BuildPrefab(sprites, controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[태양곰 임시 그림] 파츠·애니메이션·프리팹 갱신 완료");
    }

    #region 그림

    private enum Px { Empty, Fur, FurDark, FurLight, Crescent, Muzzle, Claw, Eye, Nose }

    private static readonly Dictionary<Px, Color32> Palette = new()
    {
        { Px.Fur, new Color32(74, 48, 34, 255) },
        { Px.FurDark, new Color32(54, 35, 25, 255) },
        { Px.FurLight, new Color32(100, 68, 47, 255) },
        { Px.Crescent, new Color32(238, 178, 92, 255) },
        { Px.Muzzle, new Color32(196, 150, 104, 255) },
        { Px.Claw, new Color32(236, 224, 200, 255) },
        { Px.Eye, new Color32(18, 10, 8, 255) },
        { Px.Nose, new Color32(30, 18, 14, 255) },
    };
    private static readonly Color32 Outline = new(34, 21, 15, 255);

    private class PixelCanvas
    {
        public readonly int W, H;
        private readonly Px[] cells;

        public PixelCanvas(int w, int h)
        {
            W = w; H = h;
            cells = new Px[w * h];
        }

        public Px Get(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? Px.Empty : cells[y * W + x];
        public void Set(int x, int y, Px value) { if (x >= 0 && y >= 0 && x < W && y < H) cells[y * W + x] = value; }

        public void Ellipse(float cx, float cy, float rx, float ry, Px value, bool onlyFilled = false)
        {
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                if (dx * dx + dy * dy > 1f) continue;
                if (onlyFilled && Get(x, y) == Px.Empty) continue;
                Set(x, y, value);
            }
        }

        public void Rect(int x0, int y0, int x1, int y1, Px value)
        {
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++) Set(x, y, value);
        }

        // 위쪽에 밝은 털, 아래쪽에 어두운 털 (몸 색인 칸만)
        public void Shade(Px baseFur)
        {
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (Get(x, y) != baseFur) continue;
                if (Get(x, y + 2) == Px.Empty && Get(x, y + 1) != Px.Empty) Set(x, y, baseFur == Px.FurDark ? Px.Fur : Px.FurLight);
                else if (Get(x, y - 2) == Px.Empty && Get(x, y - 1) != Px.Empty && baseFur == Px.Fur) Set(x, y, Px.FurDark);
            }
        }

        // 바깥 경계 칸은 외곽선 색 (그림 크기를 키우지 않음)
        public Color32[] Render()
        {
            var result = new Color32[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Px cell = Get(x, y);
                if (cell == Px.Empty) continue;
                bool edge = Get(x - 1, y) == Px.Empty || Get(x + 1, y) == Px.Empty || Get(x, y - 1) == Px.Empty || Get(x, y + 1) == Px.Empty;
                result[y * W + x] = edge && cell != Px.Claw ? Outline : Palette[cell];
            }
            return result;
        }
    }

    private static Dictionary<string, Sprite> BuildSprites()
    {
        Directory.CreateDirectory(SpriteFolder);
        var result = new Dictionary<string, Sprite>
        {
            ["TorsoStand"] = Save("TorsoStand", DrawTorsoStand(), new Vector2(0.5f, 0.05f)),
            ["TorsoQuad"] = Save("TorsoQuad", DrawTorsoQuad(), new Vector2(0.5f, 0.5f)),
            ["Head"] = Save("Head", DrawHead(), new Vector2(0.6f, 0.05f)),
            ["ArmFront"] = Save("ArmFront", DrawArm(Px.Fur), new Vector2(0.5f, 0.95f)),
            ["ArmBack"] = Save("ArmBack", DrawArm(Px.FurDark), new Vector2(0.5f, 0.95f)),
            ["LegFront"] = Save("LegFront", DrawLeg(Px.Fur), new Vector2(0.5f, 0.95f)),
            ["LegBack"] = Save("LegBack", DrawLeg(Px.FurDark), new Vector2(0.5f, 0.95f)),
        };
        return result;
    }

    private static PixelCanvas DrawTorsoStand()
    {
        var c = new PixelCanvas(30, 40);
        c.Ellipse(15f, 20f, 14.5f, 19.5f, Px.Fur);
        c.Shade(Px.Fur);
        // 가슴의 초승달 무늬 (U자)
        c.Ellipse(11f, 27f, 8f, 5f, Px.Crescent, true);
        c.Ellipse(11f, 30f, 7f, 4.5f, Px.Fur, true);
        return c;
    }

    private static PixelCanvas DrawTorsoQuad()
    {
        var c = new PixelCanvas(52, 26);
        c.Ellipse(26f, 13f, 25.5f, 12.5f, Px.Fur);
        c.Shade(Px.Fur);
        // 엎드리면 가슴 무늬가 앞쪽 아래로 보임
        c.Ellipse(9f, 11f, 5f, 8f, Px.Crescent, true);
        c.Ellipse(12f, 11f, 5f, 7f, Px.Fur, true);
        return c;
    }

    private static PixelCanvas DrawHead()
    {
        var c = new PixelCanvas(26, 22);
        c.Ellipse(8f, 18.5f, 3.2f, 3.2f, Px.Fur);
        c.Ellipse(19f, 18.5f, 3.2f, 3.2f, Px.Fur);
        c.Ellipse(14f, 10f, 10f, 9.5f, Px.Fur);
        c.Shade(Px.Fur);
        c.Ellipse(5.5f, 7f, 5.5f, 3.8f, Px.Muzzle);
        c.Rect(1, 7, 2, 8, Px.Nose);
        c.Rect(9, 12, 9, 13, Px.Eye);
        return c;
    }

    private static PixelCanvas DrawArm(Px fur)
    {
        var c = new PixelCanvas(10, 26);
        c.Rect(1, 5, 8, 23, fur);
        c.Ellipse(5f, 22.5f, 4.5f, 3.5f, fur);
        c.Ellipse(4.5f, 4f, 4.5f, 4f, fur);
        c.Shade(fur);
        c.Rect(0, 0, 0, 1, Px.Claw);
        c.Rect(2, 0, 2, 0, Px.Claw);
        c.Rect(4, 0, 4, 0, Px.Claw);
        return c;
    }

    private static PixelCanvas DrawLeg(Px fur)
    {
        var c = new PixelCanvas(12, 22);
        c.Rect(2, 4, 10, 19, fur);
        c.Ellipse(6.5f, 19f, 5f, 3.5f, fur);
        c.Ellipse(5f, 2.5f, 5f, 2.5f, fur);
        c.Shade(fur);
        c.Rect(0, 0, 0, 0, Px.Claw);
        return c;
    }

    private static Sprite Save(string part, PixelCanvas canvas, Vector2 pivot)
    {
        string path = $"{SpriteFolder}/SunBear_{part}.png";
        var tex = new Texture2D(canvas.W, canvas.H, TextureFormat.RGBA32, false);
        tex.SetPixels32(canvas.Render());
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    #endregion

    #region 자세

    // 파츠별 (x, y, 회전) 픽셀 좌표. 회전은 도 (팔다리는 0이면 아래로, 음수면 앞(왼쪽)으로 들림)
    private class Pose
    {
        public readonly Dictionary<string, Vector3> parts = new();
        public bool quad;

        public Pose Clone()
        {
            var p = new Pose { quad = quad };
            foreach (var (k, v) in parts) p.parts[k] = v;
            return p;
        }

        public Pose Set(string part, float x, float y, float rot) { var p = Clone(); p.parts[part] = new Vector3(x, y, rot); return p; }
        public Pose Rot(string part, float rot) { var p = Clone(); Vector3 v = p.parts[part]; p.parts[part] = new Vector3(v.x, v.y, rot); return p; }
        public Pose Move(string part, float dx, float dy) { var p = Clone(); Vector3 v = p.parts[part]; p.parts[part] = new Vector3(v.x + dx, v.y + dy, v.z); return p; }

        // 여러 파츠를 함께 옮김 (몸을 숙이거나 젖힘)
        public Pose MoveAll(float dx, float dy, params string[] names)
        {
            Pose p = this;
            foreach (string n in names) p = p.Move(n, dx, dy);
            return p;
        }
    }

    private static readonly string[] Upper = { "TorsoStand", "TorsoQuad", "Head", "ArmFront", "ArmBack" };

    private static Pose Stand()
    {
        var p = new Pose { quad = false };
        p.parts["LegBack"] = new Vector3(5, 22, 0);
        p.parts["LegFront"] = new Vector3(-3, 22, 0);
        p.parts["TorsoStand"] = new Vector3(0, 20, 0);
        p.parts["TorsoQuad"] = new Vector3(0, 26, 0);
        p.parts["ArmBack"] = new Vector3(5, 52, 0);
        p.parts["ArmFront"] = new Vector3(-5, 52, 0);
        p.parts["Head"] = new Vector3(-5, 57, 0);
        return p;
    }

    private static Pose Quad()
    {
        var p = new Pose { quad = true };
        p.parts["TorsoStand"] = new Vector3(0, 20, 0);
        p.parts["TorsoQuad"] = new Vector3(0, 26, 0);
        p.parts["Head"] = new Vector3(-26, 26, 0);
        p.parts["ArmFront"] = new Vector3(-16, 27, 0);
        p.parts["ArmBack"] = new Vector3(-11, 27, 0);
        p.parts["LegFront"] = new Vector3(15, 23, 0);
        p.parts["LegBack"] = new Vector3(20, 23, 0);
        return p;
    }

    private static Pose Walk(float legSwing, float armSwing, bool flip)
    {
        float s = flip ? -1f : 1f;
        return Stand().Rot("LegFront", -legSwing * s).Rot("LegBack", legSwing * s)
            .Rot("ArmFront", armSwing * s).Rot("ArmBack", -armSwing * s);
    }

    private static Pose Gallop(bool stretched, float dy = 0f)
    {
        Pose p = Quad();
        p = stretched
            ? p.Rot("ArmFront", -40).Rot("ArmBack", -30).Rot("LegFront", 40).Rot("LegBack", 30)
            : p.Rot("ArmFront", 25).Rot("ArmBack", 15).Rot("LegFront", -25).Rot("LegBack", -15);
        return p.MoveAll(0, dy, "TorsoQuad", "Head");
    }

    private static Pose Crouch(Pose p, float dy) => p.MoveAll(0, -dy, Upper);

    #endregion

    #region 애니메이션

    // (이름, 반복 여부, (시간, 자세) 목록)
    private static List<(string name, bool loop, (float t, Pose pose)[] keys)> Clips()
    {
        Pose s = Stand();
        Pose clawLeftUp = s.Rot("ArmFront", 150).Move("Head", 1, 0);
        Pose clawLeftHit = s.Rot("ArmFront", -50).MoveAll(-3, 0, "Head", "TorsoStand");
        Pose clawRightUp = s.Rot("ArmBack", 150).Move("Head", 1, 0);
        Pose clawRightHit = s.Rot("ArmBack", -50).MoveAll(-3, 0, "Head", "TorsoStand");
        Pose clawBothUp = s.Rot("ArmFront", 160).Rot("ArmBack", 160).Move("Head", 2, 1);
        Pose clawBothHit = s.Rot("ArmFront", -60).Rot("ArmBack", -60).MoveAll(-4, 0, "Head", "TorsoStand", "ArmFront", "ArmBack");
        Pose slamUp = s.Rot("ArmFront", -175).Rot("ArmBack", -170).Move("Head", 1, 2);
        Pose slamHit = Crouch(s, 6).Rot("ArmFront", -25).Rot("ArmBack", -20).Move("Head", -4, -2);
        Pose grabUp = s.Rot("ArmFront", -170).Rot("ArmBack", -150);
        Pose grabHold = s.Rot("ArmFront", -95).Rot("ArmBack", -85).Move("Head", -2, 0);
        Pose roarUp = s.Rot("ArmFront", 30).Rot("ArmBack", 35).Set("Head", -3, 59, -15);
        Pose roar = s.Rot("ArmFront", 45).Rot("ArmBack", 50).Set("Head", -7, 58, -20);
        Pose rockBend = Crouch(s, 14).Rot("ArmFront", -15).Rot("ArmBack", -10).Move("Head", -5, 0);
        Pose rockHold = s.Rot("ArmFront", -170).Rot("ArmBack", -165).Move("Head", 1, 1);
        Pose rockThrow = s.Rot("ArmFront", -40).Rot("ArmBack", -45).MoveAll(-3, 0, "Head", "TorsoStand");
        Pose quadCrouch = Crouch(Quad(), 3).Rot("ArmFront", 20).Rot("ArmBack", 15).Rot("LegFront", -20).Rot("LegBack", -15);

        return new()
        {
            ("Idle", true, new[] { (0f, s), (0.6f, s.MoveAll(0, -1, "Head", "ArmFront", "ArmBack")), (1.2f, s) }),
            ("Wander", true, new[] { (0f, Walk(15, 12, false)), (0.4f, Walk(15, 12, true)), (0.8f, Walk(15, 12, false)) }),
            ("Chase", true, new[] { (0f, Walk(25, 22, false)), (0.25f, Walk(25, 22, true).MoveAll(0, 1, Upper)), (0.5f, Walk(25, 22, false)) }),
            ("Windup", false, new[] { (0f, s), (0.3f, grabUp) }),
            ("Attack", false, new[] { (0f, grabUp), (0.1f, clawBothHit) }),
            ("Recover", false, new[] { (0f, clawBothHit), (0.5f, s) }),
            ("Stunned", true, new[] { (0f, s.Set("Head", -5, 55, 15)), (0.5f, s.Set("Head", -4, 55, -10)), (1f, s.Set("Head", -5, 55, 15)) }),

            ("ClawLeftWindup", false, new[] { (0f, s), (0.2f, clawLeftUp) }),
            ("ClawLeft", false, new[] { (0f, clawLeftUp), (0.1f, clawLeftHit) }),
            ("ClawRightWindup", false, new[] { (0f, clawLeftHit), (0.2f, clawRightUp) }),
            ("ClawRight", false, new[] { (0f, clawRightUp), (0.1f, clawRightHit) }),
            ("ClawBothWindup", false, new[] { (0f, clawRightHit), (0.3f, clawBothUp) }),
            ("ClawBoth", false, new[] { (0f, clawBothUp), (0.1f, clawBothHit) }),
            ("ClawRecover", false, new[] { (0f, clawBothHit), (0.6f, s) }),

            ("SlamWindup", false, new[] { (0f, s), (0.4f, slamUp) }),
            ("Slam", false, new[] { (0f, slamUp), (0.08f, slamHit) }),
            ("SlamRecover", false, new[] { (0f, slamHit), (0.5f, slamHit), (1f, s) }),

            ("ChargeWindup", false, new[] { (0f, s), (0.25f, quadCrouch) }),
            ("Charge", true, new[] { (0f, Gallop(true)), (0.18f, Gallop(false, 2)), (0.36f, Gallop(true)) }),
            ("ChargeRecover", false, new[] { (0f, Quad()), (0.6f, Quad()), (1f, s) }),

            ("LeapWindup", false, new[] { (0f, s), (0.3f, quadCrouch) }),
            ("Leap", false, new[] { (0f, quadCrouch), (0.15f, Gallop(true, 2)) }),
            ("LeapLand", false, new[] { (0f, quadCrouch), (0.3f, Quad()) }),
            ("LeapRecover", false, new[] { (0f, Quad()), (0.5f, s) }),

            ("GrabWindup", false, new[] { (0f, s), (0.4f, grabUp) }),
            ("GrabHold", true, new[] { (0f, grabHold), (0.15f, grabHold.Rot("ArmFront", -90).Rot("ArmBack", -80)), (0.3f, grabHold) }),
            ("GrabRelease", false, new[] { (0f, grabHold), (0.3f, s) }),
            ("GrabThrow", false, new[] { (0f, grabHold), (0.1f, grabUp.Move("Head", -2, 0)), (0.6f, s) }),
            ("GrabMiss", false, new[] { (0f, grabHold), (0.3f, slamHit), (1f, s) }),

            ("RoarWindup", false, new[] { (0f, s), (0.5f, roarUp) }),
            ("Roar", true, new[] { (0f, roar), (0.1f, roar.MoveAll(1, 0, "Head", "TorsoStand")), (0.2f, roar) }),

            ("RockPull", false, new[] { (0f, s), (0.4f, rockBend), (1f, rockHold) }),
            ("RockThrow", false, new[] { (0f, rockHold), (0.12f, rockThrow) }),
            ("RockRecover", false, new[] { (0f, rockThrow), (0.6f, s) }),
        };
    }

    private static AnimatorController BuildAnimations()
    {
        Directory.CreateDirectory(AnimFolder);

        string controllerPath = $"{AnimFolder}/{ControllerName}.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("IsFront", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsWandering", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        var states = new Dictionary<string, AnimatorState>();
        foreach (var (name, loop, keys) in Clips())
        {
            AnimationClip clip = BuildClip(name, loop, keys);
            AnimatorState state = machine.AddState(ControllerName + name);
            state.motion = clip;
            state.writeDefaultValues = true;
            states[name] = state;
        }
        machine.defaultState = states["Idle"];

        // 배회는 IsWandering으로 전환 (MonsterAI가 매 프레임 설정)
        AnimatorStateTransition toWander = states["Idle"].AddTransition(states["Wander"]);
        toWander.AddCondition(AnimatorConditionMode.If, 0f, "IsWandering");
        toWander.hasExitTime = false;
        toWander.duration = 0.1f;
        AnimatorStateTransition toIdle = states["Wander"].AddTransition(states["Idle"]);
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsWandering");
        toIdle.hasExitTime = false;
        toIdle.duration = 0.1f;

        return controller;
    }

    private static AnimationClip BuildClip(string name, bool loop, (float t, Pose pose)[] keys)
    {
        string path = $"{AnimFolder}/{ControllerName}{name}.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();
        clip.frameRate = 12f;

        foreach (string part in Parts)
        {
            string bindingPath = $"Root/{part}";
            var x = new AnimationCurve();
            var y = new AnimationCurve();
            var r = new AnimationCurve();
            foreach (var (t, pose) in keys)
            {
                Vector3 v = pose.parts[part];
                x.AddKey(Linear(t, v.x / PixelsPerUnit));
                y.AddKey(Linear(t, v.y / PixelsPerUnit));
                r.AddKey(Linear(t, v.z));
            }
            Smooth(x); Smooth(y); Smooth(r);
            clip.SetCurve(bindingPath, typeof(Transform), "m_LocalPosition.x", x);
            clip.SetCurve(bindingPath, typeof(Transform), "m_LocalPosition.y", y);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(bindingPath, typeof(Transform), "localEulerAnglesRaw.z"), r);
        }

        // 이족·사족 몸통을 바꿔 끼움 (계단 곡선)
        var stand = new AnimationCurve();
        var quad = new AnimationCurve();
        foreach (var (t, pose) in keys)
        {
            stand.AddKey(new Keyframe(t, pose.quad ? 0f : 1f));
            quad.AddKey(new Keyframe(t, pose.quad ? 1f : 0f));
        }
        for (int i = 0; i < stand.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(stand, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(stand, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyLeftTangentMode(quad, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(quad, i, AnimationUtility.TangentMode.Constant);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Root/TorsoStand", typeof(GameObject), "m_IsActive"), stand);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Root/TorsoQuad", typeof(GameObject), "m_IsActive"), quad);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static Keyframe Linear(float t, float v) => new(t, v);

    private static void Smooth(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
    }

    #endregion

    #region 프리팹

    private static void BuildPrefab(Dictionary<string, Sprite> sprites, AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform visual = root.transform.Find("Root");
            Material material = null;
            foreach (string old in OldParts)
            {
                Transform t = visual.Find(old);
                if (t == null) continue;
                if (material == null && t.TryGetComponent(out SpriteRenderer oldRenderer)) material = oldRenderer.sharedMaterial;
                Object.DestroyImmediate(t.gameObject);
            }
            foreach (string part in Parts)
            {
                Transform t = visual.Find(part);
                if (t == null) continue;
                if (material == null && t.TryGetComponent(out SpriteRenderer oldRenderer)) material = oldRenderer.sharedMaterial;
                Object.DestroyImmediate(t.gameObject);
            }

            Pose stand = Stand();
            var renderers = new List<SpriteRenderer>();
            for (int i = 0; i < Parts.Length; i++)
            {
                string part = Parts[i];
                var go = new GameObject(part);
                go.layer = root.layer;
                go.transform.SetParent(visual, false);
                go.transform.SetSiblingIndex(i);
                Vector3 p = stand.parts[part];
                go.transform.localPosition = new Vector3(p.x / PixelsPerUnit, p.y / PixelsPerUnit, 0f);
                go.transform.localEulerAngles = new Vector3(0f, 0f, p.z);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = sprites[part];
                if (material != null) r.sharedMaterial = material;
                r.sortingOrder = i;
                renderers.Add(r);
                if (part == "TorsoQuad") go.SetActive(false);
            }

            // 피격 반짝임 대상, 애니메이터, 그림자·충돌 크기, 상태 아이콘 위치 (보스 루트 배율 18 기준)
            var flash = new SerializedObject(root.GetComponent<HitFlash>());
            SerializedProperty targets = flash.FindProperty("targets");
            targets.arraySize = renderers.Count;
            for (int i = 0; i < renderers.Count; i++) targets.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            flash.ApplyModifiedPropertiesWithoutUndo();

            root.GetComponent<Animator>().runtimeAnimatorController = controller;

            var shadow = new SerializedObject(root.GetComponent<ShadowCaster>());
            shadow.FindProperty("width").floatValue = 3f;
            shadow.FindProperty("height").floatValue = 5.4f;
            shadow.ApplyModifiedPropertiesWithoutUndo();

            var box = root.GetComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.14f, 0f);
            box.size = new Vector3(0.14f, 0.28f, 0.14f);

            Transform sweat = visual.Find("SweatIcon");
            if (sweat != null) sweat.localPosition = new Vector3(-0.04f, 0.3f, 0f);
            Transform icons = root.transform.Find("StatusIcons");
            if (icons != null) icons.localPosition = new Vector3(0f, 0.36f, 0f);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    #endregion
}
