using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

// 도감 항목 종류별로 상세 내용을 구성 (수치·조합법·획득처·서식지 등)
// showDevInfo가 꺼지면 id·내부 수치처럼 개발 중에만 필요한 정보를 숨김 (출시용 도감 전환 대비)
public class CodexDetailBuilder
{
    private const string ValueColor = "#FFFFFF";
    private const string GoodColor = "#9BE37A";
    private const string BadColor = "#F08A7A";

    private readonly CodexDatabase db;
    private readonly CodexDetailView view;
    private readonly Action<CodexEntry> navigate;

    public bool ShowDevInfo { get; set; }

    public CodexDetailBuilder(CodexDatabase db, CodexDetailView view, Action<CodexEntry> navigate)
    {
        this.db = db;
        this.view = view;
        this.navigate = navigate;
    }

    public void Build(CodexEntry entry)
    {
        view.Clear();

        switch (entry.category)
        {
            case CodexCategory.Item: BuildItem(entry); break;
            case CodexCategory.Prop: BuildProp(entry); break;
            case CodexCategory.Structure: BuildStructure(entry); break;
            case CodexCategory.Monster: BuildMonster(entry); break;
            case CodexCategory.Biome: BuildBiome(entry); break;
        }

        view.ScrollToTop();
    }

    // ───────── 아이템 ─────────

    private void BuildItem(CodexEntry entry)
    {
        var item = (ItemData)entry.data;
        view.SetHeader(entry.icon, entry.name, Sub(ItemKind(item), item.id), item.description);

        var stats = new List<(string, string)>();
        switch (item)
        {
            case FoodItemData food:
                if (food.hungerAmount > 0f) stats.Add(("허기 회복", $"+{food.hungerAmount:0.#}"));
                if (food.healAmount > 0f) stats.Add(("체력 회복", $"+{food.healAmount:0.#}"));
                break;
            case ToolItemData tool:
                stats.Add(("도구 종류", tool.toolType == ToolType.None ? "무기" : ToolName(tool.toolType)));
                stats.Add(("티어", tool.tier.ToString()));
                stats.Add(("피해", tool.damage.ToString()));
                break;
            case PlaceableItemData placeable:
                stats.Add(("내구도", $"{placeable.maxHealth:0.#}"));
                stats.Add(("약점", WeaknessText(placeable.toolWeakness)));
                stats.Add(("설치 거리", $"{placeable.placeRange:0.#}m"));
                if (placeable.alignToPlayer) stats.Add(("놓는 방향", "플레이어를 향해 가로로"));
                break;
        }
        stats.Add(("최대 겹치기", $"{item.maxStack}개"));
        view.Section("정보");
        view.Text(Stats(stats));

        if (item is PlaceableItemData placeableItem)
        {
            CodexEntry structure = db.Find(placeableItem.placedPrefab);
            if (structure != null) LinkLine(structure, $"설치하면 <b>{structure.name}</b>");
        }

        if (item is ToolItemData toolItem) BuildToolTargets(toolItem);

        IReadOnlyList<Recipe> recipes = db.GetRecipesFor(item);
        if (recipes.Count > 0)
        {
            view.Section("조합법");
            foreach (Recipe recipe in recipes) RecipeRow(recipe);
        }

        IReadOnlyList<Recipe> usedIn = db.GetRecipesUsing(item);
        if (usedIn.Count > 0)
        {
            view.Section("재료로 사용");
            foreach (Recipe recipe in usedIn) RecipeRow(recipe);
        }

        IReadOnlyList<CodexDatabase.DropSource> sources = db.GetDropSources(item);
        if (sources.Count > 0)
        {
            view.Section("획득처");
            foreach (CodexDatabase.DropSource source in sources)
            {
                LinkLine(source.source, $"{source.source.name} · {DropText(source.drop)}");
            }
        }

        if (recipes.Count == 0 && sources.Count == 0)
        {
            view.Section("획득처");
            view.Text(Color("아직 얻을 방법이 없음", BadColor));
        }
    }

    // 이 도구가 약점인 자원·설치물 (배율이 붙는 대상)
    private void BuildToolTargets(ToolItemData tool)
    {
        if (tool.toolType == ToolType.None) return;

        var targets = new List<(CodexEntry entry, ToolWeakness weakness, int requiredTier)>();
        foreach (CodexEntry entry in db.GetEntries(CodexCategory.Prop))
        {
            var prop = (PropData)entry.data;
            if (prop.toolWeakness.tool == tool.toolType) targets.Add((entry, prop.toolWeakness, prop.requiredTier));
        }
        foreach (CodexEntry entry in db.GetEntries(CodexCategory.Structure))
        {
            var placeable = (PlaceableItemData)entry.data;
            if (placeable.toolWeakness.tool == tool.toolType) targets.Add((entry, placeable.toolWeakness, 0));
        }
        if (targets.Count == 0) return;

        view.Section("효과적인 대상");
        foreach (var (entry, weakness, requiredTier) in targets)
        {
            bool tierOk = requiredTier == 0 || tool.tier >= requiredTier;
            string text = tierOk
                ? $"{entry.name} · 피해 ×{weakness.multiplier:0.##}"
                : $"{entry.name} · {Color($"티어 {requiredTier} 필요", BadColor)}";
            LinkLine(entry, text);
        }
    }

    // ───────── 자원 ─────────

    private void BuildProp(CodexEntry entry)
    {
        var prop = (PropData)entry.data;
        view.SetHeader(entry.icon, entry.name, Sub("자원", prop.id), prop.description);

        var stats = new List<(string, string)>
        {
            ("체력", $"{prop.maxHealth:0.#}"),
            ("약점", WeaknessText(prop.toolWeakness)),
            ("채집 조건", prop.requiredTier > 0
                ? $"{ToolName(prop.toolWeakness.tool)} 티어 {prop.requiredTier} 이상"
                : "제한 없음"),
        };
        if (ShowDevInfo && prop.destroyEffectPrefab != null) stats.Add(("파괴 이펙트", prop.destroyEffectPrefab.name));
        view.Section("정보");
        view.Text(Stats(stats));

        view.Section("도구별 채집");
        if (prop.requiredTier > 0) view.Text(Color("맨손·다른 도구·몬스터 공격으로는 피해를 받지 않음", BadColor));
        foreach (ToolItemData tool in db.Tools)
        {
            bool canHarvest = prop.requiredTier == 0
                || (tool.toolType == prop.toolWeakness.tool && tool.tier >= prop.requiredTier);
            HarvestLine(tool, canHarvest, prop.toolWeakness, prop.maxHealth);
        }

        DropSection("드랍", prop.dropTable);
        HabitatSection(entry);
    }

    // ───────── 설치물 ─────────

    private void BuildStructure(CodexEntry entry)
    {
        var item = (PlaceableItemData)entry.data;
        view.SetHeader(entry.icon, entry.name, Sub("설치물", item.id), item.description);

        var stats = new List<(string, string)>
        {
            ("내구도", $"{item.maxHealth:0.#}"),
            ("약점", WeaknessText(item.toolWeakness)),
            ("설치 거리", $"{item.placeRange:0.#}m"),
        };
        view.Section("정보");
        view.Text(Stats(stats));

        CodexEntry itemEntry = db.Find(item);
        if (itemEntry != null) LinkLine(itemEntry, $"설치 아이템: <b>{itemEntry.name}</b>");

        if (entry.prefab.TryGetComponent(out CraftingStation station))
        {
            view.Section("제작대");
            view.Text($"반경 {Color($"{station.Range:0.#}m", ValueColor)} 안에 있으면 아래 레시피를 만들 수 있음");
            IReadOnlyList<Recipe> recipes = db.GetRecipesAtStation(item);
            if (recipes.Count == 0) view.Text(Color("이 설치물이 필요한 레시피가 없음", BadColor));
            foreach (Recipe recipe in recipes) RecipeRow(recipe);
        }

        view.Section("도구별 파괴");
        foreach (ToolItemData tool in db.Tools)
        {
            HarvestLine(tool, true, item.toolWeakness, item.maxHealth);
        }

        DropSection("파괴 시 드랍", item.dropTable);
    }

    // ───────── 몬스터 ─────────

    private void BuildMonster(CodexEntry entry)
    {
        var data = (MonsterData)entry.data;
        view.SetHeader(entry.icon, entry.name, Sub("몬스터", data.monsterName), data.description);

        var stats = new List<(string, string)>
        {
            ("체력", $"{data.maxHealth:0.#}"),
            ("공격", data.attack.damage > 0 ? $"피해 {data.attack.damage} · 넉백 {data.attack.knockbackDistance:0.#}" : "피해 없음"),
            ("이동 속도", $"{data.moveSpeed:0.#} (추격 {data.chaseSpeed:0.#})"),
            ("넉백 저항", $"{data.knockbackResistance * 100f:0}%"),
            ("공격 중 경직", data.superArmorWhileAttacking ? Color("없음 (슈퍼아머)", BadColor) : "있음"),
        };
        view.Section("능력치");
        view.Text(Stats(stats));

        if (ShowDevInfo)
        {
            view.Section("행동 수치");
            view.Text(Stats(new List<(string, string)>
            {
                ("배회", $"범위 {data.wanderRange:0.#} · {data.wanderCooldown:0.#}초마다"),
                ("감지 / 포기 반경", $"{data.chaseRange:0.#} / {data.chaseGiveUpRange:0.#}"),
                ("공격 준비", $"거리 {data.windupRange:0.#} · {data.windupDuration:0.##}초"),
                ("공격", $"속도 {data.attackSpeed:0.#} · {data.attackDuration:0.##}초"),
                ("공격 후 회복", $"{data.recoverDuration:0.##}초"),
                ("땀 표시", $"체력 {data.lowHealthRatio * 100f:0}% 이하"),
            }));

            List<(string, string)> unique = UniqueFields(data);
            if (unique.Count > 0)
            {
                view.Section("고유 수치");
                view.Text(Stats(unique));
            }
        }

        view.Section("적대 관계");
        bool any = false;
        if (data.hostileTargets != null)
        {
            foreach (MonsterData target in data.hostileTargets)
            {
                CodexEntry targetEntry = db.Find(target);
                if (targetEntry == null) continue;
                LinkLine(targetEntry, $"{targetEntry.name}을(를) {Color("공격함", BadColor)}");
                any = true;
            }
        }
        foreach (CodexEntry hunter in db.GetHunters(data))
        {
            LinkLine(hunter, $"{hunter.name}에게 {Color("공격받음", BadColor)}");
            any = true;
        }
        if (!any) view.Text("다른 몬스터와 싸우지 않음");

        DropSection("드랍", data.dropTable);
        HabitatSection(entry);
    }

    // 하위 클래스(RoseviperData 등)에만 있는 공개 필드를 그대로 나열 (새 몬스터가 생겨도 자동 표시)
    private static List<(string, string)> UniqueFields(MonsterData data)
    {
        var result = new List<(string, string)>();
        for (Type type = data.GetType(); type != typeof(MonsterData) && type != null; type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                result.Add((Nicify(field.Name), FormatValue(field.GetValue(data))));
            }
        }
        return result;
    }

    // ───────── 지형 ─────────

    private void BuildBiome(CodexEntry entry)
    {
        var biome = (BiomeData)entry.data;
        view.SetHeader(entry.icon, entry.name, Sub("지형", biome.id), biome.description);

        if (ShowDevInfo)
        {
            view.Section("생성 수치");
            view.Text(Stats(new List<(string, string)>
            {
                ("판정 범위", $"{biome.minThreshold:0.##} ~ {biome.maxThreshold:0.##}"),
                ("텍스처 반복", $"{biome.tilingScale:0.##}"),
                ("경계 블렌드", $"{biome.blendRange:0.##}"),
            }));
        }

        if (biome.propSpawnTable != null && biome.propSpawnTable.Length > 0)
        {
            view.Section("자원 분포");
            foreach (PropSpawnEntry spawn in biome.propSpawnTable)
            {
                CodexEntry prop = db.Find(spawn.propPrefab);
                if (prop == null) continue;

                string text = $"{prop.name} · {PropCountText(spawn)}";
                if (ShowDevInfo)
                {
                    text += $" · 간격 {spawn.minSpacing:0.#}";
                    if (spawn.clusterRadius > 0f) text += $" · 군집 반경 {spawn.clusterRadius:0.#}";
                    if (spawn.countWeights != null && spawn.countWeights.Length > 0) text += $" · {CountWeightsText(spawn)}";
                }
                LinkLine(prop, text);
            }
        }

        if (biome.monsterSpawnTable != null && biome.monsterSpawnTable.Length > 0)
        {
            float totalWeight = 0f;
            foreach (MonsterSpawnEntry spawn in biome.monsterSpawnTable) totalWeight += spawn.weight;

            view.Section("몬스터 분포");
            foreach (MonsterSpawnEntry spawn in biome.monsterSpawnTable)
            {
                CodexEntry monster = spawn.monsterPrefab != null ? db.Find(spawn.monsterPrefab.gameObject) : null;
                if (monster == null) continue;

                float chance = totalWeight > 0f ? spawn.weight / totalWeight : 0f;
                LinkLine(monster, $"{monster.name} · 출현 {chance * 100f:0}%");
            }
        }
    }

    // ───────── 공용 섹션 ─────────

    private void HabitatSection(CodexEntry entry)
    {
        IReadOnlyList<CodexDatabase.Habitat> habitats = db.GetHabitats(entry);
        view.Section("서식지");
        if (habitats.Count == 0)
        {
            view.Text(Color("자연적으로 생성되지 않음", BadColor));
            return;
        }

        foreach (CodexDatabase.Habitat habitat in habitats)
        {
            string text = entry.category == CodexCategory.Monster
                ? $"{habitat.biome.name} · 출현 {habitat.monsterChance * 100f:0}%"
                : $"{habitat.biome.name} · {PropCountText(habitat.propEntry)}";
            LinkLine(habitat.biome, text);
        }
    }

    private void DropSection(string title, DropTable table)
    {
        view.Section(title);
        if (table == null || table.Drops.Count == 0)
        {
            view.Text("없음");
            return;
        }

        foreach (ItemDrop drop in table.Drops)
        {
            if (drop.item == null) continue;

            CodexEntry itemEntry = db.Find(drop.item);
            string text = $"{drop.item.displayName} · {DropText(drop)}";
            if (itemEntry != null) LinkLine(itemEntry, text);
            else view.Line(drop.item.icon, text, null);
        }
    }

    // 도구 하나로 몇 번 때려야 부서지는지
    private void HarvestLine(ToolItemData tool, bool canDamage, ToolWeakness weakness, float maxHealth)
    {
        string text;
        if (!canDamage)
        {
            text = $"{tool.displayName} · {Color("채집 불가", BadColor)}";
        }
        else
        {
            var attack = new AttackData { damage = tool.damage, toolType = tool.toolType, toolTier = tool.tier };
            int damage = weakness.CalculateDamage(attack);
            string hits = damage > 0 ? $"{Mathf.CeilToInt(maxHealth / damage)}회" : "-";
            bool weak = weakness.tool != ToolType.None && tool.toolType == weakness.tool;
            text = $"{tool.displayName} · {Color($"{damage} 피해", weak ? GoodColor : ValueColor)} · {hits}";
        }

        CodexEntry toolEntry = db.Find(tool);
        if (toolEntry != null) LinkLine(toolEntry, text);
        else view.Line(tool.icon, text, null);
    }

    // [재료 + 재료 → 결과 @ 스테이션] 한 줄
    private void RecipeRow(Recipe recipe)
    {
        RectTransform row = view.Row();
        for (int i = 0; i < recipe.ingredients.Length; i++)
        {
            Ingredient ingredient = recipe.ingredients[i];
            if (ingredient.item == null) continue;

            if (i > 0) view.InlineText(row, "+");
            ItemChip(row, ingredient.item, $"{ingredient.item.displayName} ×{ingredient.count}");
        }

        view.InlineText(row, "→");
        ItemChip(row, recipe.result, $"{recipe.result.displayName} ×{recipe.resultCount}");

        if (recipe.requiredStation != null)
        {
            view.InlineText(row, "@");
            ItemChip(row, recipe.requiredStation, recipe.requiredStation.displayName);
        }
    }

    private void ItemChip(RectTransform row, ItemData item, string text)
    {
        CodexEntry entry = db.Find(item);
        view.Chip(row, item.icon, text, entry != null ? () => navigate(entry) : null);
    }

    private void LinkLine(CodexEntry target, string text)
    {
        view.Line(target.icon, text, () => navigate(target));
    }

    // ───────── 문자열 ─────────

    private string Sub(string kind, string id)
    {
        return ShowDevInfo && !string.IsNullOrEmpty(id) ? $"{kind} · {id}" : kind;
    }

    // "이름<pos>값" 줄을 이어 붙여 표처럼 정렬
    private static string Stats(List<(string label, string value)> rows)
    {
        var builder = new StringBuilder();
        foreach (var (label, value) in rows)
        {
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(label).Append("<pos=38%>").Append(Color(value, ValueColor));
        }
        return builder.ToString();
    }

    private static string Color(string text, string color) => $"<color={color}>{text}</color>";

    private static string Range(int min, int max) => min == max ? min.ToString() : $"{min}~{max}";

    private static string PropCountText(PropSpawnEntry spawn)
    {
        spawn.GetCountRange(out int min, out int max);
        string text = $"청크당 {Range(min, max)}개";

        float chance = spawn.AppearChance();
        if (chance < 1f) text += $" (출현 {chance * 100f:0.#}%)";
        return text;
    }

    // 개수별 확률 (예: 0개 80% / 1개 15% / 2개 5%)
    private static string CountWeightsText(PropSpawnEntry spawn)
    {
        float total = 0f;
        foreach (PropCountWeight w in spawn.countWeights) total += Mathf.Max(0f, w.weight);
        if (total <= 0f) return "가중치 없음";

        var parts = new List<string>();
        foreach (PropCountWeight w in spawn.countWeights)
        {
            if (w.weight <= 0f) continue;
            parts.Add($"{w.count}개 {w.weight / total * 100f:0.#}%");
        }
        return string.Join(" / ", parts);
    }

    private static string DropText(ItemDrop drop)
    {
        string text = $"{Range(drop.minCount, drop.maxCount)}개";
        if (drop.chance < 1f) text += $" ({drop.chance * 100f:0.#}%)";
        return text;
    }

    private static string WeaknessText(ToolWeakness weakness)
    {
        if (weakness.tool == ToolType.None) return "없음";
        return $"{ToolName(weakness.tool)} 피해 ×{weakness.multiplier:0.##}";
    }

    private static string ItemKind(ItemData item) => item switch
    {
        FoodItemData => "음식",
        ToolItemData tool => tool.toolType == ToolType.None ? "무기" : "도구",
        PlaceableItemData => "설치 아이템",
        _ => "재료",
    };

    private static string ToolName(ToolType type) => type switch
    {
        ToolType.Axe => "도끼",
        ToolType.Pickaxe => "곡괭이",
        ToolType.Hammer => "망치",
        _ => "없음",
    };

    private static string FormatValue(object value) => value switch
    {
        null => "없음",
        float f => f.ToString("0.##"),
        bool b => b ? "예" : "아니오",
        AttackData attack => $"피해 {attack.damage} · 넉백 {attack.knockbackDistance:0.#}",
        UnityEngine.Object obj => obj != null ? obj.name : "없음",
        _ => value.ToString(),
    };

    // attackHitDuration → Attack Hit Duration
    private static string Nicify(string name)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i == 0) builder.Append(char.ToUpperInvariant(c));
            else
            {
                if (char.IsUpper(c)) builder.Append(' ');
                builder.Append(c);
            }
        }
        return builder.ToString();
    }
}
