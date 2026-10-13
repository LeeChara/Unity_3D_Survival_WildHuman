using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// 월드 구조물 프리팹의 루트 (StructureSpawner가 청크 로드 때 만들고 언로드 때 상태를 저장한 뒤 지움)
// 자식 부품 중 SaveableEntity가 붙은 것(상자 등)의 상태를 부품 이름 경로를 키로 저장 → 프리팹을 고쳐 순서가 바뀌어도 상태가 엉뚱한 부품에 붙지 않음
// 부서져 사라진 부품은 null로 기록해서 다시 로드해도 생기지 않음
// 구조물 전체의 상태(퍼즐 해결 등)는 루트 오브젝트의 ISaveable이 "root" 칸에 저장
[DisallowMultipleComponent]
public class WorldStructure : MonoBehaviour
{
    private const string PiecesKey = "pieces";
    private const string RootKey = "root";

    // 부품 키 → 부품 (사라졌으면 Unity null)
    private readonly Dictionary<string, SaveableEntity> pieces = new();
    private ISaveable[] rootSaveables;

    private void Awake()
    {
        CollectPieces();
    }

    private void CollectPieces()
    {
        pieces.Clear();
        foreach (var entity in GetComponentsInChildren<SaveableEntity>(true))
        {
            if (entity.transform == transform) continue;

            string key = PathOf(entity.transform);
            if (pieces.ContainsKey(key))
            {
                Debug.LogWarning($"[WorldStructure] '{name}'에 이름 경로가 같은 부품이 있음: {key} (상태가 저장되지 않음)", this);
                continue;
            }
            pieces[key] = entity;
        }
        rootSaveables = GetComponents<ISaveable>();
    }

    public JObject Capture()
    {
        var piecesState = new JObject();
        foreach (var (key, entity) in pieces)
        {
            if (entity == null)
            {
                piecesState[key] = JValue.CreateNull();
                continue;
            }

            Dictionary<string, JToken> data = entity.Capture().data;
            if (data.Count > 0) piecesState[key] = JObject.FromObject(data);
        }

        var state = new JObject();
        if (piecesState.Count > 0) state[PiecesKey] = piecesState;

        var rootState = new JObject();
        foreach (var saveable in rootSaveables)
        {
            JToken token = saveable.Save();
            if (token != null) rootState[saveable.SaveKey] = token;
        }
        if (rootState.Count > 0) state[RootKey] = rootState;

        return state.Count > 0 ? state : null;
    }

    // 없는 키(새로 추가된 부품)는 프리팹 기본 상태 그대로, 지금 프리팹에 없는 키는 무시
    public void Restore(JObject state)
    {
        if (state == null) return;

        if (state[PiecesKey] is JObject piecesState)
        {
            foreach (var (key, token) in piecesState)
            {
                if (!pieces.TryGetValue(key, out SaveableEntity entity) || entity == null) continue;

                if (token == null || token.Type == JTokenType.Null)
                {
                    // 부서진 부품: 이벤트 없이 바로 없앰 (Destroy는 프레임 끝에 일어나므로 지금 꺼서 보이지 않게)
                    entity.gameObject.SetActive(false);
                    Destroy(entity.gameObject);
                    pieces[key] = null;
                    continue;
                }

                if (token is JObject data) entity.LoadData(new EntityRecord { data = data.ToObject<Dictionary<string, JToken>>() });
            }
        }

        if (state[RootKey] is JObject rootState)
        {
            foreach (var saveable in rootSaveables)
            {
                if (rootState.TryGetValue(saveable.SaveKey, out JToken token)) saveable.Load(token);
            }
        }
    }

    private string PathOf(Transform piece)
    {
        var names = new List<string>();
        for (Transform t = piece; t != null && t != transform; t = t.parent)
        {
            names.Add(t.name);
        }
        names.Reverse();
        return string.Join("/", names);
    }

#if UNITY_EDITOR
    // 이름 경로가 겹치는 부품이 있으면 프리팹 편집 중에 바로 알림
    private void OnValidate()
    {
        var seen = new HashSet<string>();
        foreach (var entity in GetComponentsInChildren<SaveableEntity>(true))
        {
            if (entity.transform == transform) continue;
            if (!seen.Add(PathOf(entity.transform))) Debug.LogWarning($"[WorldStructure] '{name}' 부품 이름 경로 중복: {PathOf(entity.transform)}", entity);
        }
    }
#endif
}
