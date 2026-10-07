using System.Collections.Generic;
using UnityEngine;

// 세이브의 id로 에셋을 찾는 목록 (아이템, 저장 대상 프리팹)
// 인스펙터 우측 상단 메뉴의 [자동 수집]으로 프로젝트 전체에서 채움
[CreateAssetMenu(fileName = "SaveRegistry", menuName = "Save/SaveRegistry")]
public class SaveRegistry : ScriptableObject
{
    [SerializeField] private ItemData[] items;
    [SerializeField] private SaveableEntity[] entities;

    // WorldSaveManager가 씬 시작 시 지정 (인벤토리 등 개별 컴포넌트가 참조 없이 찾을 수 있도록)
    public static SaveRegistry Active { get; set; }

    private Dictionary<string, ItemData> itemsById;
    private Dictionary<string, SaveableEntity> entitiesById;

    // 세이브에 있지만 지금은 없는 id면 null
    public ItemData GetItem(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        itemsById ??= BuildLookup(items, item => item.id);
        if (itemsById.TryGetValue(id, out ItemData item)) return item;

        Debug.LogWarning($"[SaveRegistry] 아이템 '{id}'를 찾지 못함");
        return null;
    }

    public SaveableEntity GetEntity(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        entitiesById ??= BuildLookup(entities, entity => entity.TypeId);
        if (entitiesById.TryGetValue(id, out SaveableEntity entity)) return entity;

        Debug.LogWarning($"[SaveRegistry] 개체 '{id}'를 찾지 못함");
        return null;
    }

    private static Dictionary<string, T> BuildLookup<T>(T[] assets, System.Func<T, string> getId) where T : Object
    {
        var lookup = new Dictionary<string, T>();
        if (assets == null) return lookup;

        foreach (var asset in assets)
        {
            if (asset == null) continue;

            string id = getId(asset);
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"[SaveRegistry] '{asset.name}'의 id가 비어 있음", asset);
                continue;
            }
            if (lookup.ContainsKey(id))
            {
                Debug.LogWarning($"[SaveRegistry] id '{id}'가 중복됨 ('{lookup[id].name}', '{asset.name}')", asset);
                continue;
            }
            lookup[id] = asset;
        }
        return lookup;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        itemsById = null;
        entitiesById = null;
    }

    [ContextMenu("자동 수집")]
    private void Collect()
    {
        var foundItems = new List<ItemData>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ItemData"))
        {
            var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (item != null) foundItems.Add(item);
        }

        var foundEntities = new List<SaveableEntity>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab"))
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && prefab.TryGetComponent(out SaveableEntity entity)) foundEntities.Add(entity);
        }

        items = foundItems.ToArray();
        entities = foundEntities.ToArray();
        OnValidate();
        UnityEditor.EditorUtility.SetDirty(this);

        // 비어 있거나 중복된 id를 콘솔에 경고
        BuildLookup(items, item => item.id);
        BuildLookup(entities, entity => entity.TypeId);
        Debug.Log($"[SaveRegistry] 아이템 {items.Length}개, 개체 {entities.Length}개 수집");
    }
#endif
}
