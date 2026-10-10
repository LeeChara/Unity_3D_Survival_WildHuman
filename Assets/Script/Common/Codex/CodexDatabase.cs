using System.Collections.Generic;
using UnityEngine;

public enum CodexCategory
{
    Item,
    Prop,
    Structure,
    Monster,
    Biome,
}

// 도감에 표시할 모든 데이터의 목록과, 데이터 사이의 연결(획득처·사용처·서식지 등)
// 인스펙터 우측 상단 메뉴의 [자동 수집]으로 프로젝트 전체에서 채움
// 연결 정보는 처음 쓸 때 한 번만 계산함
[CreateAssetMenu(fileName = "CodexDatabase", menuName = "Codex/CodexDatabase")]
public class CodexDatabase : ScriptableObject
{
    [SerializeField] private ItemData[] items;
    [SerializeField] private RecipeBook recipeBook;
    [SerializeField] private CookingBook cookingBook;
    [SerializeField] private PropHealth[] props;
    [SerializeField] private StructureHealth[] structures;
    [SerializeField] private MonsterAI[] monsters;
    [SerializeField] private BiomeData[] biomes;

    // 드랍 테이블을 가진 대상 하나와 그 항목
    public readonly struct DropSource
    {
        public readonly CodexEntry source;
        public readonly ItemDrop drop;

        public DropSource(CodexEntry source, ItemDrop drop)
        {
            this.source = source;
            this.drop = drop;
        }
    }

    // 바이옴 스폰 테이블의 한 줄 (Prop은 propEntry, 몬스터는 monsterChance만 유효)
    public readonly struct Habitat
    {
        public readonly CodexEntry biome;
        public readonly PropSpawnEntry propEntry;
        // 같은 바이옴 안에서 이 몬스터가 뽑힐 확률 (0~1)
        public readonly float monsterChance;

        public Habitat(CodexEntry biome, PropSpawnEntry propEntry, float monsterChance)
        {
            this.biome = biome;
            this.propEntry = propEntry;
            this.monsterChance = monsterChance;
        }
    }

    private static readonly IReadOnlyList<Recipe> NoRecipes = new Recipe[0];
    private static readonly IReadOnlyList<CookingRecipe> NoCooking = new CookingRecipe[0];
    private static readonly IReadOnlyList<DropSource> NoDrops = new DropSource[0];
    private static readonly IReadOnlyList<Habitat> NoHabitats = new Habitat[0];

    private bool built;
    private readonly Dictionary<CodexCategory, List<CodexEntry>> entries = new();
    // 데이터·프리팹 어느 쪽으로도 항목을 찾을 수 있도록 둘 다 등록
    private readonly Dictionary<Object, CodexEntry> entryByKey = new();
    private readonly Dictionary<ItemData, List<Recipe>> recipesFor = new();
    private readonly Dictionary<ItemData, List<Recipe>> usedIn = new();
    private readonly Dictionary<ItemData, List<Recipe>> stationRecipes = new();
    private readonly Dictionary<ItemData, List<CookingRecipe>> cookingFor = new();
    private readonly Dictionary<ItemData, List<CookingRecipe>> cookingUsing = new();
    private readonly Dictionary<ItemData, List<DropSource>> dropSources = new();
    private readonly Dictionary<CodexEntry, List<Habitat>> habitats = new();

    // 굽기 레시피 전체 (모닥불처럼 CampfireCooker가 있는 설치물에서 사용)
    public IReadOnlyList<CookingRecipe> CookingRecipes => cookingBook != null ? cookingBook.Recipes : NoCooking;

    // 굽기를 하는 설치물의 아이템 (레시피 줄의 '@ 모닥불' 표시용, 없으면 null)
    public ItemData CookingStation
    {
        get
        {
            Build();
            return cookingStation;
        }
    }
    private ItemData cookingStation;

    public IReadOnlyList<ToolItemData> Tools => tools;
    private readonly List<ToolItemData> tools = new();

    public IReadOnlyList<CodexEntry> GetEntries(CodexCategory category)
    {
        Build();
        return entries[category];
    }

    // 도감에 없는 대상이면 null (링크를 걸지 않음)
    public CodexEntry Find(Object key)
    {
        if (key == null) return null;
        Build();
        return entryByKey.TryGetValue(key, out CodexEntry entry) ? entry : null;
    }

    public IReadOnlyList<Recipe> GetRecipesFor(ItemData item) => Get(recipesFor, item, NoRecipes);
    public IReadOnlyList<Recipe> GetRecipesUsing(ItemData item) => Get(usedIn, item, NoRecipes);
    public IReadOnlyList<Recipe> GetRecipesAtStation(ItemData stationItem) => Get(stationRecipes, stationItem, NoRecipes);
    public IReadOnlyList<CookingRecipe> GetCookingFor(ItemData item) => Get(cookingFor, item, NoCooking);
    public IReadOnlyList<CookingRecipe> GetCookingUsing(ItemData item) => Get(cookingUsing, item, NoCooking);
    public IReadOnlyList<DropSource> GetDropSources(ItemData item) => Get(dropSources, item, NoDrops);
    public IReadOnlyList<Habitat> GetHabitats(CodexEntry entry) => Get(habitats, entry, NoHabitats);

    // 이 몬스터를 적대 대상으로 삼는 몬스터들
    public IEnumerable<CodexEntry> GetHunters(MonsterData target)
    {
        foreach (CodexEntry entry in GetEntries(CodexCategory.Monster))
        {
            var data = (MonsterData)entry.data;
            if (data.hostileTargets == null) continue;
            if (System.Array.IndexOf(data.hostileTargets, target) >= 0) yield return entry;
        }
    }

    private IReadOnlyList<TValue> Get<TKey, TValue>(Dictionary<TKey, List<TValue>> map, TKey key, IReadOnlyList<TValue> empty)
    {
        if (key == null) return empty;
        Build();
        return map.TryGetValue(key, out List<TValue> list) ? list : empty;
    }

    private void Build()
    {
        if (built) return;
        built = true;

        foreach (CodexCategory category in System.Enum.GetValues(typeof(CodexCategory)))
        {
            entries[category] = new List<CodexEntry>();
        }

        BuildEntries();
        BuildRecipeLinks();
        BuildCookingLinks();
        BuildDropLinks();
        BuildHabitatLinks();
    }

    private void BuildEntries()
    {
        foreach (ItemData item in NonNull(items))
        {
            Add(new CodexEntry(CodexCategory.Item, item.displayName, item.icon, item, null), item);
            if (item is ToolItemData tool) tools.Add(tool);
        }

        foreach (PropHealth prop in NonNull(props))
        {
            if (prop.Data == null) continue;
            Add(new CodexEntry(CodexCategory.Prop, prop.Data.displayName, PrefabSprite(prop), prop.Data, prop.gameObject),
                prop.Data, prop.gameObject);
        }

        foreach (StructureHealth structure in NonNull(structures))
        {
            PlaceableItemData item = structure.SourceItem;
            if (item == null) continue;
            Add(new CodexEntry(CodexCategory.Structure, item.displayName, PrefabSprite(structure), item, structure.gameObject),
                structure.gameObject);
        }

        foreach (MonsterAI monster in NonNull(monsters))
        {
            MonsterData data = monster.Data;
            if (data == null) continue;
            Sprite icon = data.portrait != null ? data.portrait : PrefabSprite(monster);
            Add(new CodexEntry(CodexCategory.Monster, data.displayName, icon, data, monster.gameObject),
                data, monster.gameObject);
        }

        foreach (BiomeData biome in NonNull(biomes))
        {
            string name = string.IsNullOrEmpty(biome.biomeName) ? biome.name : biome.biomeName;
            Add(new CodexEntry(CodexCategory.Biome, name, BiomeSprite(biome), biome, null), biome);
        }
    }

    private void Add(CodexEntry entry, params Object[] keys)
    {
        entries[entry.category].Add(entry);
        foreach (Object key in keys)
        {
            entryByKey.TryAdd(key, entry);
        }
    }

    private void BuildRecipeLinks()
    {
        if (recipeBook == null) return;

        foreach (Recipe recipe in recipeBook.Recipes)
        {
            if (recipe.result == null) continue;

            AddTo(recipesFor, recipe.result, recipe);
            if (recipe.requiredStation != null) AddTo(stationRecipes, recipe.requiredStation, recipe);

            foreach (Ingredient ingredient in recipe.ingredients)
            {
                // 같은 레시피에 같은 재료가 두 줄이어도 사용처에는 한 번만 표시
                if (ingredient.item == null) continue;
                if (usedIn.TryGetValue(ingredient.item, out List<Recipe> list) && list.Contains(recipe)) continue;
                AddTo(usedIn, ingredient.item, recipe);
            }
        }
    }

    private void BuildCookingLinks()
    {
        foreach (StructureHealth structure in NonNull(structures))
        {
            if (!structure.TryGetComponent(out CampfireCooker _)) continue;
            cookingStation = structure.SourceItem;
            break;
        }

        if (cookingBook == null) return;

        foreach (CookingRecipe recipe in cookingBook.Recipes)
        {
            if (recipe.input == null || recipe.result == null) continue;
            AddTo(cookingFor, recipe.result, recipe);
            AddTo(cookingUsing, recipe.input, recipe);
        }
    }

    private void BuildDropLinks()
    {
        foreach (CodexEntry entry in entries[CodexCategory.Prop]) AddDrops(entry, ((PropData)entry.data).dropTable);
        foreach (CodexEntry entry in entries[CodexCategory.Structure]) AddDrops(entry, ((PlaceableItemData)entry.data).dropTable);
        foreach (CodexEntry entry in entries[CodexCategory.Monster]) AddDrops(entry, ((MonsterData)entry.data).dropTable);
    }

    private void AddDrops(CodexEntry source, DropTable table)
    {
        if (table == null) return;

        foreach (ItemDrop drop in table.Drops)
        {
            if (drop.item != null) AddTo(dropSources, drop.item, new DropSource(source, drop));
        }
    }

    private void BuildHabitatLinks()
    {
        foreach (CodexEntry biomeEntry in entries[CodexCategory.Biome])
        {
            var biome = (BiomeData)biomeEntry.data;

            if (biome.propSpawnTable != null)
            {
                foreach (PropSpawnEntry spawn in biome.propSpawnTable)
                {
                    CodexEntry prop = Find(spawn.propPrefab);
                    if (prop != null) AddTo(habitats, prop, new Habitat(biomeEntry, spawn, 0f));
                }
            }

            if (biome.monsterSpawnTable != null)
            {
                float totalWeight = 0f;
                foreach (MonsterSpawnEntry spawn in biome.monsterSpawnTable) totalWeight += spawn.weight;

                foreach (MonsterSpawnEntry spawn in biome.monsterSpawnTable)
                {
                    CodexEntry monster = spawn.monsterPrefab != null ? Find(spawn.monsterPrefab.gameObject) : null;
                    if (monster == null) continue;

                    float chance = totalWeight > 0f ? spawn.weight / totalWeight : 0f;
                    AddTo(habitats, monster, new Habitat(biomeEntry, default, chance));
                }
            }
        }
    }

    private static void AddTo<TKey, TValue>(Dictionary<TKey, List<TValue>> map, TKey key, TValue value)
    {
        if (!map.TryGetValue(key, out List<TValue> list))
        {
            list = new List<TValue>();
            map[key] = list;
        }
        list.Add(value);
    }

    private static IEnumerable<T> NonNull<T>(T[] array) where T : Object
    {
        if (array == null) yield break;
        foreach (T value in array)
        {
            if (value != null) yield return value;
        }
    }

    // 프리팹에서 가장 큰 스프라이트 (자원·설치물은 보통 스프라이트가 하나뿐)
    private static Sprite PrefabSprite(Component prefab)
    {
        Sprite best = null;
        float bestArea = 0f;
        foreach (SpriteRenderer renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sprite == null || !renderer.gameObject.activeSelf) continue;

            float area = renderer.sprite.rect.width * renderer.sprite.rect.height;
            if (area > bestArea)
            {
                best = renderer.sprite;
                bestArea = area;
            }
        }
        return best;
    }

    // 바닥 텍스처를 그대로 아이콘으로 사용 (Sprite.Create는 Read/Write 설정 없이도 동작)
    private static Sprite BiomeSprite(BiomeData biome)
    {
        Texture2D texture = biome.planeTexture;
        if (texture == null) return null;
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        built = false;
        entries.Clear();
        entryByKey.Clear();
        recipesFor.Clear();
        usedIn.Clear();
        stationRecipes.Clear();
        cookingFor.Clear();
        cookingUsing.Clear();
        cookingStation = null;
        dropSources.Clear();
        habitats.Clear();
        tools.Clear();
    }

    [ContextMenu("자동 수집")]
    private void Collect()
    {
        items = FindAssets<ItemData>("t:ItemData");
        biomes = FindAssets<BiomeData>("t:BiomeData");
        RecipeBook[] books = FindAssets<RecipeBook>("t:RecipeBook");
        recipeBook = books.Length > 0 ? books[0] : null;
        CookingBook[] cookingBooks = FindAssets<CookingBook>("t:CookingBook");
        cookingBook = cookingBooks.Length > 0 ? cookingBooks[0] : null;

        var foundProps = new List<PropHealth>();
        var foundStructures = new List<StructureHealth>();
        var foundMonsters = new List<MonsterAI>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab"))
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) continue;

            if (prefab.TryGetComponent(out PropHealth prop)) foundProps.Add(prop);
            if (prefab.TryGetComponent(out StructureHealth structure)) foundStructures.Add(structure);
            if (prefab.TryGetComponent(out MonsterAI monster)) foundMonsters.Add(monster);
        }
        props = foundProps.ToArray();
        structures = foundStructures.ToArray();
        monsters = foundMonsters.ToArray();

        // 아이템은 종류별로 묶어서 표시 (재료 → 음식 → 도구 → 설치물)
        System.Array.Sort(items, (a, b) =>
        {
            int order = ItemOrder(a).CompareTo(ItemOrder(b));
            return order != 0 ? order : string.CompareOrdinal(a.name, b.name);
        });

        OnValidate();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[CodexDatabase] 아이템 {items.Length}, 자원 {props.Length}, 설치물 {structures.Length}, " +
                  $"몬스터 {monsters.Length}, 바이옴 {biomes.Length}, 레시피북 {(recipeBook != null ? recipeBook.name : "없음")}, " +
                  $"굽기 {(cookingBook != null ? cookingBook.name : "없음")}");
    }

    private static int ItemOrder(ItemData item) => item switch
    {
        FoodItemData => 1,
        ToolItemData => 2,
        PlaceableItemData => 3,
        _ => 0,
    };

    private static T[] FindAssets<T>(string filter) where T : Object
    {
        var found = new List<T>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets(filter))
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) found.Add(asset);
        }
        return found.ToArray();
    }
#endif
}
