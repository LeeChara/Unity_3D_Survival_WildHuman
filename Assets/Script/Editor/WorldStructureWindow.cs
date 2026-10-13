using UnityEditor;
using UnityEngine;

// 월드 구조물 점검용 창 (개발용)
// 플레이 중인 월드의 구조물 목록을 보여주고, 누르면 플레이어를 그 근처로 순간이동
public class WorldStructureWindow : EditorWindow
{
    // 구조물 가장자리에서 이만큼 떨어진 곳으로 이동 (부품과 겹치지 않게)
    private const float WarpMargin = 3f;

    private Vector2 scroll;

    [MenuItem("Tools/월드/구조물 목록")]
    private static void Open()
    {
        GetWindow<WorldStructureWindow>("월드 구조물");
    }

    // 플레이어가 움직이면 거리가 바뀌므로 계속 다시 그림
    private void OnInspectorUpdate()
    {
        if (Application.isPlaying) Repaint();
    }

    private void OnGUI()
    {
        WorldState world = Application.isPlaying ? WorldState.Instance : null;
        if (world == null)
        {
            EditorGUILayout.HelpBox("플레이 중에만 현재 월드의 구조물을 볼 수 있음", MessageType.Info);
            return;
        }

        var player = FindAnyObjectByType<PlayerHealth>();
        Vector3 from = player != null ? player.transform.position : Vector3.zero;

        EditorGUILayout.LabelField($"구조물 {world.Map.structures.Count}개", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < world.Map.structures.Count; i++)
        {
            StructureSite site = world.Map.structures[i];
            WorldStructureData data = world.GetStructure(site);
            Vector3 position = site.Position();
            float distance = Vector3.Distance(new Vector3(from.x, 0f, from.z), position);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{i}. {(data != null ? data.id : "(없는 종류)")}", GUILayout.Width(180f));
            EditorGUILayout.LabelField($"({site.x:F0}, {site.z:F0})  {distance:F0}m  {(site.state != null ? "상태 있음" : "")}");
            GUI.enabled = player != null;
            if (GUILayout.Button("이동", GUILayout.Width(50f)))
            {
                float radius = data != null ? data.radius : 0f;
                Vector3 target = position + Vector3.back * (radius + WarpMargin);
                target.y = from.y;
                player.WarpTo(target);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }
}
