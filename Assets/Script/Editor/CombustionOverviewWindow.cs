using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 연소 규칙 점검용 창 (개발용, 플레이어에게는 가연성을 표시하지 않음)
// 프리팹을 가연물(Combustible)·폭발물(ExplosiveKeg)·화염원(FireSource)·타지 않음으로 나눠 보여주고, 누르면 프리팹을 선택함
public class CombustionOverviewWindow : EditorWindow
{
    private class Row
    {
        public GameObject prefab;
        public string detail;
    }

    private readonly List<Row> combustibles = new();
    private readonly List<Row> explosives = new();
    private readonly List<Row> sources = new();
    private readonly List<Row> nonCombustibles = new();
    private CombustionSetting setting;
    private Vector2 scroll;

    [MenuItem("Tools/연소/가연물 목록")]
    private static void Open()
    {
        GetWindow<CombustionOverviewWindow>("연소 점검").Refresh();
    }

    private void OnFocus() => Refresh();

    private void Refresh()
    {
        combustibles.Clear();
        explosives.Clear();
        sources.Clear();
        nonCombustibles.Clear();
        setting = null;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) continue;

            if (prefab.TryGetComponent(out Combustible combustible))
            {
                StatusEffectData burn = combustible.Burn;
                string detail = burn != null
                    ? $"{burn.name} · {burn.tickInterval:0.##}초마다 {burn.tickDamage} · {burn.duration:0.#}초"
                    : "burn 상태 없음!";
                combustibles.Add(new Row { prefab = prefab, detail = detail });
                if (setting == null) setting = combustible.Setting;
            }
            else if (prefab.TryGetComponent(out ExplosiveKeg _))
            {
                explosives.Add(new Row { prefab = prefab, detail = "도화선 → 폭발" });
            }
            else if (prefab.TryGetComponent(out Health _))
            {
                nonCombustibles.Add(new Row { prefab = prefab, detail = "" });
            }

            // 불타는 동안만 켜지는 가연물의 화염원은 제외 (항상 켜진 화염원만)
            if (prefab.TryGetComponent(out FireSource source) && source.enabled && combustible == null)
            {
                sources.Add(new Row { prefab = prefab, detail = prefab.GetComponent<IHazardArea>() != null ? "모양 가장자리" : "몸체 주변" });
            }
        }
    }

    private void OnGUI()
    {
        if (GUILayout.Button("새로고침")) Refresh();

        if (setting != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"전이 거리 {setting.spreadDistance:0.##}m · 간격 {setting.spreadInterval:0.##}초");
            if (GUILayout.Button("설정 열기", GUILayout.Width(80f))) Selection.activeObject = setting;
            EditorGUILayout.EndHorizontal();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        Section("가연물 (Combustible)", combustibles);
        Section("폭발물", explosives);
        Section("항상 켜진 화염원", sources);
        Section("타지 않음 (체력 있음)", nonCombustibles);
        EditorGUILayout.EndScrollView();
    }

    private static void Section(string title, List<Row> rows)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"{title} ({rows.Count})", EditorStyles.boldLabel);
        foreach (Row row in rows)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(row.prefab.name, EditorStyles.linkLabel, GUILayout.Width(180f)))
            {
                Selection.activeObject = row.prefab;
                EditorGUIUtility.PingObject(row.prefab);
            }
            EditorGUILayout.LabelField(row.detail);
            EditorGUILayout.EndHorizontal();
        }
    }
}
