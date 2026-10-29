#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Data;
using NevrianGlowFarm.Logic.Text;
using UnityEditor;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 기획 CSV 가져오기 (기획 확정 반영 2026-10-21, 회신 0절 "수치의 단일 원본 = 기획 CSV" · 회신2 5 "열 이름은 기획 CSV 기준").
    /// 저장소 `CSV/plan/`(data_csv · shop_csv · skill_csv)을 기획 열 이름 그대로 읽고, `ID_MAP.md`로 개발 id에 연결해 에셋 값만 바꾼다.
    /// 여러 번 실행해도 결과가 같다. 개발 에셋이 없는 행(시스템 대기)은 건너뛰고 결과 문서 `CSV/plan/가져오기_결과.md`에 남긴다.
    /// 새로 만드는 에셋은 스킬뿐이다 (콘텐츠 추가 = 에셋 추가) — 아이템 · 상품은 시스템이 생길 때 그 작업에서 만든다.
    /// </summary>
    public static class PlanCsvImporter
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string SkillsFolder = DataRoot + "/Skills";
        private const string StatsFolder = DataRoot + "/Stats";
        private const string TutorialFolder = DataRoot + "/Tutorial";
        internal const string TutorialAssetPath = TutorialFolder + "/TutorialSteps.asset";
        private const string TutorialCsv = "data_csv/tutorial_steps.csv";
        private const int BedCells = 4; // GDD 1페이지: 밭 1개 = 2×2칸

        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string PlanRoot => Path.Combine(RepoRoot, "CSV", "plan");
        private static string IdMapPath => Path.Combine(RepoRoot, "ID_MAP.md");
        private static string ReportPath => Path.Combine(PlanRoot, "가져오기_결과.md");

        internal sealed class Report
        {
            public readonly List<string> Updated = new List<string>();
            public readonly List<string> Created = new List<string>();
            public readonly List<string> Hidden = new List<string>();
            public readonly List<string> Skipped = new List<string>();
            public readonly List<string> Waiting = new List<string>();
            public readonly List<string> Problems = new List<string>();
            public int Phase2;

            public string Summary => $"갱신 {Updated.Count} · 생성 {Created.Count} · 숨김 {Hidden.Count} · 건너뜀 {Skipped.Count} · 2차 {Phase2} · 시스템 대기 설정 {Waiting.Count} · 문제 {Problems.Count}";
        }

        [MenuItem("Glow Farm/Data/Import Plan CSV (기획 CSV 가져오기)")]
        private static void ImportMenu()
        {
            var report = Run();
            if (report == null)
                return;
            string line = "[PlanCsv] " + report.Summary + " — 자세한 내용: CSV/plan/가져오기_결과.md";
            if (report.Problems.Count > 0)
                Debug.LogWarning(line);
            else
                Debug.Log(line);
        }

        /// <summary>가져오기를 실행한다. 파일이 없으면 null.</summary>
        internal static Report Run()
        {
            var report = new Report();
            if (!TryLoad(out var t, out var map, out string error))
            {
                Debug.LogError("[PlanCsv] " + error);
                return null;
            }

            var byId = LoadContent();
            ImportItems(t, map, byId, report);
            ImportCrops(t, map, byId, report);
            int plotsAdded = ImportShop(t, map, byId, report);
            ImportSkills(t, map, byId, report);
            ImportSettings(t, map, byId, plotsAdded, report);
            ImportTutorial(t, report);

            AssetDatabase.SaveAssets();
            ContentDatabaseBuilder.Refresh();
            WriteReport(report);
            return report;
        }

        // ── 읽기 ─────────────────────────────────────────

        private sealed class Tables
        {
            public CsvTable Items, Crops, Shop, Skills, Effects, Tabs, Settings;
            /// <summary>튜토리얼 단계 (5-7) — 예전 기획 묶음에는 없을 수 있어 null 허용.</summary>
            public CsvTable Tutorial;
        }

        private static bool TryLoad(out Tables t, out IdMap map, out string error)
        {
            t = new Tables();
            map = null;
            error = null;
            try
            {
                t.Items = Read("data_csv/items.csv");
                t.Crops = Read("data_csv/crops.csv");
                t.Settings = Read("data_csv/game_settings.csv");
                t.Shop = Read("shop_csv/shop.csv");
                t.Skills = Read("skill_csv/skills.csv");
                t.Effects = Read("skill_csv/skill_effects.csv");
                t.Tabs = Read("skill_csv/skill_tabs.csv");
                t.Tutorial = File.Exists(Path.Combine(PlanRoot, TutorialCsv)) ? Read(TutorialCsv) : null;
                map = IdMap.Parse(File.ReadAllText(IdMapPath, Encoding.UTF8));
                return true;
            }
            catch (Exception e)
            {
                error = $"기획 CSV · ID_MAP.md를 읽지 못함 ({PlanRoot}): {e.Message}";
                return false;
            }
        }

        private static CsvTable Read(string relative) => CsvTable.Parse(File.ReadAllText(Path.Combine(PlanRoot, relative), Encoding.UTF8));

        private static Dictionary<string, ContentData> LoadContent()
        {
            var dict = new Dictionary<string, ContentData>();
            foreach (var guid in AssetDatabase.FindAssets("t:ContentData", new[] { DataRoot }))
            {
                var c = AssetDatabase.LoadAssetAtPath<ContentData>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null && !string.IsNullOrEmpty(c.Id))
                    dict[c.Id] = c;
            }
            return dict;
        }

        private static bool IsPhase2(CsvTable table, int row) => table.Get(row, "mvp") == "phase2";

        private static T Find<T>(Dictionary<string, ContentData> byId, string devId) where T : ContentData =>
            devId != null && byId.TryGetValue(devId, out var c) ? c as T : null;

        // ── 아이템 (items.csv) ──────────────────────────

        private static void ImportItems(Tables t, IdMap map, Dictionary<string, ContentData> byId, Report report)
        {
            var table = t.Items;
            for (int r = 0; r < table.RowCount; r++)
            {
                string planId = table.Get(r, "item_id");
                if (IsPhase2(table, r)) { report.Phase2++; continue; }
                var entry = map.Find(planId);
                string devId = map.DevIdOf(planId);
                var item = Find<ItemData>(byId, devId);
                if (item == null)
                {
                    report.Skipped.Add($"아이템 {planId} — {(entry != null && entry.Status == IdMapStatus.None ? "개발 에셋 없음 (ID_MAP)" : "개발 에셋을 찾지 못함")}");
                    continue;
                }

                var so = new SerializedObject(item);
                so.FindProperty("displayName").stringValue = table.Get(r, "name_ko");
                if (CsvValue.TryInt(table.Get(r, "stack_size"), out int stack))
                    so.FindProperty("maxStack").intValue = Mathf.Max(1, stack);

                switch (table.Get(r, "item_type"))
                {
                    case "furniture":
                        // 과일 진열대 한 대에 놓는 개수 = 작물의 진열 칸당 개수 (items.csv display_per_slot — 10/20 개정 30)
                        if (item is FurnitureData && TryCropDisplayPerSlot(table, out int perShelf))
                            so.FindProperty("itemsPerLayer").intValue = perShelf;
                        break;
                    case "crop":
                        if (CsvValue.TryInt(table.Get(r, "sell_price"), out int sell))
                            Set(so, "sellPrice", sell, report, planId);
                        break;
                    case "tool":
                        CsvValue.TryFloat(table.Get(r, "tool_power"), out float power);
                        CsvValue.TryFloat(table.Get(r, "tool_radius"), out float radius);
                        CsvValue.TryFloat(table.Get(r, "tool_speed"), out float speed);
                        switch (item)
                        {
                            case HoeData _: so.FindProperty("workPerUse").floatValue = power; break;
                            case WateringCanData _: so.FindProperty("cellCount").intValue = Mathf.RoundToInt(power); break;
                            case MopData _:
                                so.FindProperty("radius").floatValue = radius;
                                so.FindProperty("speed").floatValue = speed;
                                break;
                        }
                        break;
                }
                Apply(so, item, $"아이템 {planId} → {devId}", report);
            }
        }

        // 데모 작물(phase2 제외)의 display_per_slot — 모두 같아야 한다 (과일 진열대 한 대 = 한 종류)
        private static bool TryCropDisplayPerSlot(CsvTable items, out int value)
        {
            value = 0;
            for (int r = 0; r < items.RowCount; r++)
                if (items.Get(r, "item_type") == "crop" && !IsPhase2(items, r) && CsvValue.TryInt(items.Get(r, "display_per_slot"), out int v))
                    value = Mathf.Max(value, v);
            return value > 0;
        }

        // ── 작물 (crops.csv) ────────────────────────────

        private static void ImportCrops(Tables t, IdMap map, Dictionary<string, ContentData> byId, Report report)
        {
            var table = t.Crops;
            for (int r = 0; r < table.RowCount; r++)
            {
                string planId = table.Get(r, "crop_id");
                if (IsPhase2(table, r)) { report.Phase2++; continue; }
                string devId = map.DevIdOf(planId);
                var crop = Find<CropData>(byId, devId);
                if (crop == null)
                {
                    report.Skipped.Add($"작물 {planId} — 개발 에셋을 찾지 못함");
                    continue;
                }
                // 수확물 · 씨앗 연결 확인 (개발은 작물 에셋이 수확물을 겸한다)
                if (map.DevIdOf(table.Get(r, "product_item_id")) != crop.Id)
                    report.Problems.Add($"작물 {planId}: 수확물 {table.Get(r, "product_item_id")}이 ID_MAP에서 {crop.Id}로 연결되지 않음");
                var seed = Find<SeedData>(byId, map.DevIdOf(table.Get(r, "seed_item_id")));
                if (seed == null || seed.Crop != crop)
                    report.Problems.Add($"작물 {planId}: 씨앗 {table.Get(r, "seed_item_id")}의 개발 에셋이 없거나 다른 작물을 가리킴");

                var so = new SerializedObject(crop);
                so.FindProperty("displayName").stringValue = table.Get(r, "name_ko");
                SetIntCol(so, "growDays", table, r, "grow_days", 1, report, planId);
                if (CsvValue.TryBool(table.Get(r, "needs_water"), out bool water))
                    so.FindProperty("needsWater").boolValue = water;
                if (CsvValue.TryInt(table.Get(r, "wet_days"), out int wet))
                    so.FindProperty("wetDays").intValue = Mathf.Max(1, wet); // 물 불필요 작물은 0 → 개발 최소 1 (쓰이지 않음)
                SetIntCol(so, "harvestYield", table, r, "yield_base", 1, report, planId);
                SetIntCol(so, "maxHarvests", table, r, "harvest_count", 1, report, planId);
                SetIntCol(so, "regrowDays", table, r, "regrow_days", 0, report, planId);
                CsvValue.TryBool(table.Get(r, "can_be_giant"), out bool giant);
                CsvValue.TryFloat(table.Get(r, "giant_chance"), out float chance);
                CsvValue.TryFloat(table.Get(r, "giant_yield_mult"), out float mult);
                so.FindProperty("giantChance").floatValue = giant ? Mathf.Clamp01(chance) : 0f;
                so.FindProperty("giantYieldMultiplier").floatValue = Mathf.Max(1f, mult);
                if (CsvValue.TryColor(table.Get(r, "glow_color"), out float cr, out float cg, out float cb, out float ca))
                    so.FindProperty("greyboxColor").colorValue = new Color(cr, cg, cb, ca);
                // 나무형 작물(growth_form tree)은 나무 · 열매를 조준해야 수확 (수정사항 댓글 10/29)
                so.FindProperty("harvestFromModel").boolValue = table.Get(r, "growth_form") == "tree";
                Apply(so, crop, $"작물 {planId}", report);
            }
        }

        // ── 상점 (shop.csv) ────

        /// <summary>
        /// 상품을 가져온다. 밭 확장으로 늘어나는 구획 합을 돌려준다 (최대 구획 계산).
        /// 해금 = unlock 열 — "level:2"는 레벨 조건(스킬트리 폐기 10/29). 예전 스킬 조건(skill_*:단계)도 읽지만 배울 곳이 없으므로 쓰지 않는다.
        /// skill_effects의 단계형 해금(unlock_tier)은 더 읽지 않는다 — 도구 · 가방의 모든 단계가 unlock 열 조건 하나로 열린다.
        /// </summary>
        private static int ImportShop(Tables t, IdMap map, Dictionary<string, ContentData> byId, Report report)
        {
            var table = t.Shop;
            int plotsAdded = 0;
            int cellsPerPlot = LoadSettings<GameStartSettingsData>("GameStartSettings")?.CellsPerPlot ?? 16;
            // 레벨 보너스 입고가 시작되는 레벨 (game_settings stock_bonus_level, 수정사항 10/28: 레벨 6에서 씨앗 입고 두 배)
            int bonusStockLevel = 0;
            for (int r = 0; r < t.Settings.RowCount; r++)
                if (t.Settings.Get(r, "key") == "stock_bonus_level")
                    CsvValue.TryInt(t.Settings.Get(r, "value"), out bonusStockLevel);

            for (int r = 0; r < table.RowCount; r++)
            {
                string planId = table.Get(r, "shop_id");
                var entry = map.Find(planId);
                string devId = map.DevIdOf(planId);
                var product = Find<ShopProductData>(byId, devId);
                if (IsPhase2(table, r))
                {
                    report.Phase2++;
                    if (product != null)
                        Hide(product, $"상품 {planId} (2차)", report);
                    continue;
                }
                if (product == null)
                {
                    report.Skipped.Add($"상품 {planId} ({table.Get(r, "name_ko")}) — {(entry != null ? "ID_MAP: " + entry.Status : "개발 에셋을 찾지 못함")}");
                    continue;
                }

                if (!PlanCsv.TryIntList(table.Get(r, "price_per_step"), out var prices) || prices.Count == 0
                    || !CsvValue.TryInt(table.Get(r, "max_purchases"), out int maxPurchases)
                    || !PlanCsv.TryIntList(table.Get(r, "grant_value_per_step"), out var grants))
                {
                    report.Problems.Add($"상품 {planId}: 가격 · 최대 구매 · 지급값을 읽지 못함 — 건너뜀");
                    continue;
                }
                bool repeatable = maxPurchases == 0;
                int tierCount = repeatable ? 1 : maxPurchases;
                if (prices.Count != tierCount)
                {
                    report.Problems.Add($"상품 {planId}: 가격 {prices.Count}개 ≠ 단계 {tierCount}개 — 건너뜀");
                    continue;
                }

                var so = new SerializedObject(product);
                string category = table.Get(r, "category");
                so.FindProperty("displayName").stringValue = table.Get(r, "name_ko");
                so.FindProperty("description").stringValue = UiText.ForScreenFont(table.Get(r, "description")); // 툴팁 본문 (5-4c)
                so.FindProperty("sortOrder").intValue = (r + 1) * 10;
                so.FindProperty("category").stringValue = map.DevIdOf(category, "상점 분류") ?? category;
                so.FindProperty("repeatable").boolValue = repeatable;
                bool hidden = entry != null && entry.Status == IdMapStatus.Hidden;
                so.FindProperty("hidden").boolValue = hidden;

                // 해금 조건 = unlock 열 (모든 단계 같음). level:N → 레벨 조건, 그 밖(skill_*:N) → 예전 스킬 조건
                var unlock = PlanCsv.ParseRequirements(table.Get(r, "unlock"));
                var reqs = new List<(Logic.Shop.UnlockKind Kind, string Target, int Value)>();
                foreach (var (key, value) in unlock)
                {
                    if (key == "level")
                        reqs.Add((Logic.Shop.UnlockKind.LevelAtLeast, string.Empty, value));
                    else if (map.DevIdOf(key) is string devSkill)
                    {
                        reqs.Add((Logic.Shop.UnlockKind.SkillLevel, devSkill, value));
                        report.Problems.Add($"상품 {planId}: 스킬 해금 조건 {key} — 스킬트리 폐기(10/29)로 열 수 없음, level:N으로 바꿀 것");
                    }
                }
                var tiers = so.FindProperty("tiers");
                tiers.ClearArray();
                for (int i = 0; i < tierCount; i++)
                {
                    tiers.InsertArrayElementAtIndex(i);
                    var tier = tiers.GetArrayElementAtIndex(i);
                    tier.FindPropertyRelative("price").intValue = prices[i];
                    var list = tier.FindPropertyRelative("requirements");
                    list.ClearArray();
                    foreach (var req in reqs)
                    {
                        list.InsertArrayElementAtIndex(list.arraySize);
                        var e = list.GetArrayElementAtIndex(list.arraySize - 1);
                        e.FindPropertyRelative("kind").enumValueIndex = (int)req.Kind;
                        e.FindPropertyRelative("targetId").stringValue = req.Target;
                        e.FindPropertyRelative("value").intValue = req.Value;
                    }
                }

                // 씨앗 입고: 기본 입고량. 해금이 필요한 상품은 조건을 만족한 다음 날부터 (shop README 5 · 회신 #33)
                var stock = so.FindProperty("dailyStock");
                stock.ClearArray();
                bool hasStock = CsvValue.TryInt(table.Get(r, "stock_base"), out int stockBase) && stockBase > 0;
                if (hasStock)
                {
                    stock.InsertArrayElementAtIndex(0);
                    stock.GetArrayElementAtIndex(0).intValue = stockBase;
                }
                so.FindProperty("stockFromNextDay").boolValue = hasStock && unlock.Count > 0; // 입고가 있는 상품(씨앗)만
                // 레벨 보너스 입고: stock_max가 기본보다 크면 stock_bonus_level부터 그만큼 (수정사항 10/28: 레벨 6 두 배)
                int stockMax = 0;
                bool hasBonus = hasStock && bonusStockLevel > 0 && CsvValue.TryInt(table.Get(r, "stock_max"), out stockMax) && stockMax > stockBase;
                so.FindProperty("bonusStock").intValue = hasBonus ? stockMax : 0;
                so.FindProperty("bonusStockLevel").intValue = hasBonus ? bonusStockLevel : 0;

                string grantTarget = table.Get(r, "grant_target");
                switch (product)
                {
                    case ItemProductData _:
                        var item = Find<ItemData>(byId, map.DevIdOf(grantTarget));
                        if (item == null)
                            report.Problems.Add($"상품 {planId}: 지급 아이템 {grantTarget}의 개발 에셋 없음");
                        so.FindProperty("item").objectReferenceValue = item;
                        so.FindProperty("count").intValue = grants.Count > 0 ? Mathf.Max(1, grants[0]) : 1;
                        if (item is SeedData seed && seed.Crop != null)
                        {
                            var cropSo = new SerializedObject(seed.Crop);
                            cropSo.FindProperty("seedPrice").intValue = prices[0]; // 측정 봇 · 작물 정보가 읽는 씨앗 가격
                            cropSo.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(seed.Crop);
                        }
                        break;
                    case ToolUpgradeProductData _:
                        var tools = ToolsOfKind(t.Items, map, byId, grantTarget);
                        if (tools.Count != tierCount + 1)
                        {
                            report.Problems.Add($"상품 {planId}: {grantTarget} 등급 도구 {tools.Count}개 — 처음 1 + 단계 {tierCount}개여야 함");
                            break;
                        }
                        so.FindProperty("baseTool").objectReferenceValue = tools[0];
                        var tierTools = so.FindProperty("tierTools");
                        tierTools.ClearArray();
                        for (int i = 1; i < tools.Count; i++)
                        {
                            tierTools.InsertArrayElementAtIndex(i - 1);
                            tierTools.GetArrayElementAtIndex(i - 1).objectReferenceValue = tools[i];
                        }
                        break;
                    case PlotExpandProductData _:
                        var perTier = so.FindProperty("plotsPerTier");
                        perTier.ClearArray();
                        for (int i = 0; i < grants.Count; i++)
                        {
                            if (!PlanCsv.TryPlotsFromCells(grants[i], cellsPerPlot, out int plots))
                            {
                                report.Problems.Add($"상품 {planId}: {grants[i]}칸이 구획({cellsPerPlot}칸)으로 나누어떨어지지 않음");
                                plots = Mathf.Max(1, grants[i] / cellsPerPlot);
                            }
                            perTier.InsertArrayElementAtIndex(i);
                            perTier.GetArrayElementAtIndex(i).intValue = plots;
                            plotsAdded += plots;
                        }
                        break;
                    case BagExpandProductData _:
                        if (grants.Distinct().Count() > 1)
                            report.Problems.Add($"상품 {planId}: 단계마다 다른 가방 칸 수는 아직 못 넣음 — 첫 값만");
                        so.FindProperty("slots").intValue = grants.Count > 0 ? Mathf.Max(1, grants[0]) : 1;
                        break;
                    case HotbarExpandProductData _:
                        if (grants.Distinct().Count() > 1)
                            report.Problems.Add($"상품 {planId}: 단계마다 다른 핫바 칸 수는 아직 못 넣음 — 첫 값만");
                        so.FindProperty("slots").intValue = grants.Count > 0 ? Mathf.Max(1, grants[0]) : 1;
                        break;
                }
                Apply(so, product, $"상품 {planId}{(hidden ? " (숨김)" : string.Empty)}", report);
                if (hidden)
                    report.Hidden.Add($"상품 {planId} — {NoteOf(map, planId)}");
            }

            foreach (var e in map.DevOnly())
            {
                var dev = Find<ShopProductData>(byId, e.DevId);
                if (dev != null)
                    Hide(dev, $"상품 {e.DevId} (개발만)", report);
            }
            return plotsAdded;
        }

        // grant_target "tool_hoe" → items.csv에서 tool_kind = hoe인 도구를 등급 순으로 → 개발 에셋
        private static List<ToolData> ToolsOfKind(CsvTable items, IdMap map, Dictionary<string, ContentData> byId, string grantTarget)
        {
            string kind = grantTarget.StartsWith("tool_") ? grantTarget.Substring("tool_".Length) : grantTarget;
            var rows = new List<(int Tier, string Id)>();
            for (int r = 0; r < items.RowCount; r++)
                if (items.Get(r, "tool_kind") == kind && items.Get(r, "mvp") != "phase2" && CsvValue.TryInt(items.Get(r, "tool_tier"), out int tier))
                    rows.Add((tier, items.Get(r, "item_id")));
            rows.Sort((a, b) => a.Tier.CompareTo(b.Tier));
            var tools = new List<ToolData>();
            foreach (var (_, id) in rows)
            {
                var tool = Find<ToolData>(byId, map.DevIdOf(id));
                if (tool != null)
                    tools.Add(tool);
            }
            return tools;
        }

        // ── 스킬 (skills · skill_effects · skill_tabs) ───

        private static void ImportSkills(Tables t, IdMap map, Dictionary<string, ContentData> byId, Report report)
        {
            var tabs = new Dictionary<string, (string Name, int Order)>();
            for (int r = 0; r < t.Tabs.RowCount; r++)
            {
                CsvValue.TryInt(t.Tabs.Get(r, "order"), out int order);
                tabs[t.Tabs.Get(r, "tab_id")] = (t.Tabs.Get(r, "name_ko"), order);
            }

            var effects = new Dictionary<string, List<int>>();
            for (int r = 0; r < t.Effects.RowCount; r++)
            {
                string id = t.Effects.Get(r, "skill_id");
                if (!effects.TryGetValue(id, out var list))
                    effects[id] = list = new List<int>();
                list.Add(r);
            }

            var stats = new Dictionary<string, StatData>();
            foreach (var guid in AssetDatabase.FindAssets("t:StatData", new[] { StatsFolder }))
            {
                var s = AssetDatabase.LoadAssetAtPath<StatData>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null)
                    stats[s.Id] = s;
            }

            // 1) 데모 스킬 에셋을 먼저 모두 준비 (선행 스킬 참조를 걸기 위해)
            var skills = new Dictionary<string, SkillData>();
            var table = t.Skills;
            for (int r = 0; r < table.RowCount; r++)
            {
                string planId = table.Get(r, "skill_id");
                string devId = map.DevIdOf(planId);
                var skill = Find<SkillData>(byId, devId);
                if (IsPhase2(table, r))
                {
                    report.Phase2++;
                    if (skill != null)
                        Hide(skill, $"스킬 {planId} (2차)", report);
                    continue;
                }
                if (devId == null)
                {
                    report.Skipped.Add($"스킬 {planId} — ID_MAP: 개발 에셋 없음");
                    continue;
                }
                if (skill == null)
                {
                    skill = ScriptableObject.CreateInstance<SkillData>();
                    var fresh = new SerializedObject(skill);
                    fresh.FindProperty("id").stringValue = devId;
                    fresh.FindProperty("displayName").stringValue = table.Get(r, "name_ko");
                    fresh.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(skill, $"{SkillsFolder}/{devId}.asset");
                    byId[devId] = skill;
                    report.Created.Add($"스킬 {devId}");
                }
                skills[planId] = skill;
            }

            // 2) 값 채우기
            for (int r = 0; r < table.RowCount; r++)
            {
                string planId = table.Get(r, "skill_id");
                if (!skills.TryGetValue(planId, out var skill))
                    continue;
                var entry = map.Find(planId);
                var so = new SerializedObject(skill);
                so.FindProperty("displayName").stringValue = table.Get(r, "name_ko");
                so.FindProperty("description").stringValue = UiText.ForScreenFont(table.Get(r, "description")); // 툴팁 본문 (5-4c)
                string tabId = table.Get(r, "tab_id");
                var tab = tabs.TryGetValue(tabId, out var tb) ? tb : (tabId, 99);
                so.FindProperty("tab").stringValue = tab.Item1;
                so.FindProperty("tabOrder").intValue = tab.Item2;
                CsvValue.TryInt(table.Get(r, "node_col"), out int col);
                CsvValue.TryInt(table.Get(r, "node_row"), out int row);
                so.FindProperty("order").intValue = col * 100 + row; // 해금 순서(열) → 줄
                if (!PlanCsv.TryIntList(table.Get(r, "cost_per_level"), out var costs) || costs.Count == 0)
                {
                    report.Problems.Add($"스킬 {planId}: 단계별 비용을 읽지 못함 — 건너뜀");
                    continue;
                }
                SetInts(so.FindProperty("costs"), costs);
                int maxLevel = costs.Count;

                var prereqs = PlanCsv.ParseRequirements(table.Get(r, "prerequisites"));
                SkillData prereq = null;
                int prereqLevel = 1;
                if (prereqs.Count > 0)
                {
                    skills.TryGetValue(prereqs[0].Id, out prereq);
                    prereqLevel = prereqs[0].Level;
                    if (prereq == null)
                        report.Problems.Add($"스킬 {planId}: 선행 {prereqs[0].Id}의 개발 에셋 없음");
                    if (prereqs.Count > 1)
                        report.Problems.Add($"스킬 {planId}: 선행이 {prereqs.Count}개 — 개발은 하나만 (첫 번째만 씀)");
                }
                so.FindProperty("prerequisite").objectReferenceValue = prereq;
                so.FindProperty("prerequisiteLevel").intValue = prereqLevel;

                // 효과 → 단계 설명 · 능력치 변경. 연결 안 된 효과가 있으면 숨긴다.
                var reasons = new List<string>();
                var descriptions = new string[maxLevel];
                var mods = new Dictionary<string, (StatData Stat, Logic.Game.ModifierOp Op, double[] Values)>();
                effects.TryGetValue(planId, out var effectRows);
                foreach (int e in effectRows ?? new List<int>())
                {
                    CsvValue.TryInt(t.Effects.Get(e, "level"), out int level);
                    if (level < 1 || level > maxLevel)
                    {
                        report.Problems.Add($"스킬 {planId}: 효과 단계 {level}이 최대 {maxLevel} 밖");
                        continue;
                    }
                    string kind = t.Effects.Get(e, "effect_kind");
                    string target = t.Effects.Get(e, "target_id");
                    CsvValue.TryFloat(t.Effects.Get(e, "value"), out float value);
                    if (kind == "unlock" || kind == "unlock_tier")
                    {
                        var product = Find<ShopProductData>(byId, map.DevIdOf(target));
                        if (product == null || product.Hidden)
                        {
                            reasons.Add($"해금 대상 {target} {(product == null ? "없음" : "숨김")}");
                            continue;
                        }
                        string text = kind == "unlock" ? $"{product.DisplayName} 상점에 등장" : $"{product.DisplayName} {Mathf.RoundToInt(value)}단계까지 구매 가능";
                        descriptions[level - 1] = string.IsNullOrEmpty(descriptions[level - 1]) ? text : descriptions[level - 1] + " · " + text;
                    }
                    else if (kind == "stat")
                    {
                        string statPlan = t.Effects.Get(e, "stat_id");
                        var statEntry = map.Find(statPlan);
                        string statDev = map.DevIdOf(statPlan);
                        if (statDev == null || !stats.TryGetValue(statDev, out var stat) || (statEntry != null && statEntry.Status != IdMapStatus.Linked) || !string.IsNullOrEmpty(target))
                        {
                            reasons.Add($"능력치 {statPlan}{(string.IsNullOrEmpty(target) ? string.Empty : "(" + target + ")")} 연결 전");
                            continue;
                        }
                        if (!PlanCsv.TryConvertModifier(t.Effects.Get(e, "op"), value, statEntry?.Conversion, out var op, out double devValue))
                        {
                            report.Problems.Add($"스킬 {planId}: 모르는 op '{t.Effects.Get(e, "op")}'");
                            continue;
                        }
                        if (!mods.TryGetValue(statDev, out var m))
                            mods[statDev] = m = (stat, op, Enumerable.Repeat(double.NaN, maxLevel).ToArray());
                        m.Values[level - 1] = devValue;
                    }
                    else
                        reasons.Add($"효과 종류 {kind} 연결 전");
                }

                var descList = so.FindProperty("levelDescriptions");
                descList.ClearArray();
                for (int i = 0; i < maxLevel; i++)
                {
                    descList.InsertArrayElementAtIndex(i);
                    descList.GetArrayElementAtIndex(i).stringValue = descriptions[i] ?? string.Empty;
                }
                var modList = so.FindProperty("modifiers");
                modList.ClearArray();
                foreach (var m in mods.Values)
                {
                    modList.InsertArrayElementAtIndex(modList.arraySize);
                    var me = modList.GetArrayElementAtIndex(modList.arraySize - 1);
                    me.FindPropertyRelative("stat").objectReferenceValue = m.Stat;
                    me.FindPropertyRelative("op").enumValueIndex = (int)m.Op;
                    var values = me.FindPropertyRelative("values");
                    values.ClearArray();
                    double last = double.NaN;
                    for (int i = 0; i < maxLevel; i++)
                    {
                        // 값은 그 단계의 누적값 (README) — 빠진 단계는 앞 단계 값
                        if (!double.IsNaN(m.Values[i]))
                            last = m.Values[i];
                        values.InsertArrayElementAtIndex(i);
                        values.GetArrayElementAtIndex(i).floatValue = double.IsNaN(last) ? 0f : (float)last;
                    }
                }

                bool hidden = entry != null && (entry.Status == IdMapStatus.Hidden || entry.Status == IdMapStatus.DevOnly);
                if (prereq != null && prereq.Hidden)
                    reasons.Add($"선행 {prereq.Id} 숨김"); // 선행이 숨겨지면 영영 잠긴다 (선행은 CSV에서 앞 줄 — 이미 처리됨)
                if (reasons.Count > 0)
                    hidden = true;
                so.FindProperty("hidden").boolValue = hidden;
                Apply(so, skill, $"스킬 {planId}{(hidden ? " (숨김)" : string.Empty)}", report);
                if (hidden)
                    report.Hidden.Add($"스킬 {planId} — {(reasons.Count > 0 ? string.Join(", ", reasons.Distinct()) : NoteOf(map, planId))}");
            }

            foreach (var e in map.DevOnly())
            {
                var dev = Find<SkillData>(byId, e.DevId);
                if (dev != null)
                    Hide(dev, $"스킬 {e.DevId} (개발만)", report);
            }

            // 선행 스킬이 숨겨지면 뒤 스킬은 영영 잠기므로 같이 숨긴다 (사슬이 끝날 때까지)
            for (bool changed = true; changed;)
            {
                changed = false;
                foreach (var skill in skills.Values)
                {
                    if (skill.Hidden || skill.Prerequisite == null || !skill.Prerequisite.Hidden)
                        continue;
                    Hide(skill, $"스킬 {skill.Id} — 선행 {skill.Prerequisite.Id} 숨김", report);
                    changed = true;
                }
            }
        }

        // ── 설정 (game_settings.csv) ────────────────────

        private static void ImportSettings(Tables t, IdMap map, Dictionary<string, ContentData> byId, int plotsAdded, Report report)
        {
            var values = new Dictionary<string, string>();
            for (int r = 0; r < t.Settings.RowCount; r++)
                values[t.Settings.Get(r, "key")] = t.Settings.Get(r, "value");
            var handled = new HashSet<string>();
            string V(string key)
            {
                handled.Add(key);
                return values.TryGetValue(key, out var v) ? v : null;
            }

            var start = LoadSettings<GameStartSettingsData>("GameStartSettings");
            if (start != null)
            {
                var so = new SerializedObject(start);
                if (CsvValue.TryInt(V("start_money"), out int money))
                    so.FindProperty("startingMoney").intValue = money;
                if (CsvValue.TryInt(V("start_fruit_shelves"), out int shelvesAtStart))
                    so.FindProperty("startFruitShelves").intValue = Mathf.Max(0, shelvesAtStart);
                SetStartingItems(so.FindProperty("startingHotbarItems"), V("start_hotbar"), map, byId, report, "시작 핫바");
                SetStartingItems(so.FindProperty("startingStorageItems"), V("start_storage"), map, byId, report, "시작 창고");
                SetStartingItems(so.FindProperty("startingPlantedCrops"), V("start_planted"), map, byId, report, "시작 밭 작물"); // 다 자란 채로 (수정사항 10/29)
                if (PlanCsv.SplitList(V("start_bag")).Count > 0)
                    report.Problems.Add("start_bag: 개발은 시작 가방 아이템이 없음 — 기획 값이 비어 있지 않아 무시함");
                int cellsPerPlot = start.CellsPerPlot;
                if (CsvValue.TryInt(V("start_beds"), out int beds))
                {
                    if (PlanCsv.TryPlotsFromCells(beds * BedCells, cellsPerPlot, out int plots))
                    {
                        so.FindProperty("startPlotCount").intValue = plots;
                        so.FindProperty("maxPlotCount").intValue = plots + plotsAdded;
                    }
                    else
                        report.Problems.Add($"start_beds {beds}밭({beds * BedCells}칸)이 구획({cellsPerPlot}칸)으로 나누어떨어지지 않음");
                }
                Apply(so, start, "설정 GameStartSettings", report);
            }

            var progression = LoadSettings<ProgressionData>("Progression");
            if (progression != null)
            {
                var so = new SerializedObject(progression);
                if (CsvValue.TryInt(V("level_xp_base"), out int xpBase) && CsvValue.TryInt(V("level_xp_step"), out int xpStep))
                {
                    var (devBase, devStep) = PlanCsv.LevelCurveFromPlan(xpBase, xpStep);
                    so.FindProperty("levelUpBase").intValue = devBase;
                    so.FindProperty("levelUpPerLevel").intValue = devStep;
                }
                if (CsvValue.TryInt(V("sp_per_level"), out int sp))
                    so.FindProperty("skillPointsPerLevel").intValue = sp;
                V("stock_bonus_level"); // 상점 가져오기(ImportShop)가 씀
                if (CsvValue.TryFloat(V("xp_per_revenue"), out float xp))
                    so.FindProperty("expPerRevenue").floatValue = xp;
                Apply(so, progression, "설정 Progression", report);
            }

            var sales = LoadSettings<SalesSettingsData>("SalesSettings");
            if (sales != null)
            {
                var so = new SerializedObject(sales);
                if (CsvValue.TryInt(V("buy_min"), out int buyMin))
                    so.FindProperty("purchaseMin").intValue = Mathf.Max(1, buyMin);
                if (CsvValue.TryInt(V("buy_max"), out int buyMax))
                    so.FindProperty("purchaseMax").intValue = Mathf.Max(1, buyMax);
                if (CsvValue.TryFloat(V("sale_duration"), out float duration))
                    so.FindProperty("salesDuration").floatValue = Mathf.Max(1f, duration);
                if (CsvValue.TryFloat(V("customer_spawn_interval"), out float interval))
                    so.FindProperty("spawnInterval").floatValue = Mathf.Max(0f, interval); // 장사 시간 제한 없음 (수정사항 10/28)
                if (CsvValue.TryInt(V("buy_extra_max"), out int extraMax))
                    so.FindProperty("purchaseExtraMax").intValue = Mathf.Max(1, extraMax);

                // 입소문 손님 모델 (P-2, 손님수 설정안)
                void Int(string key, string field) { if (CsvValue.TryInt(V(key), out int v)) so.FindProperty(field).intValue = v; }
                void Float(string key, string field) { if (CsvValue.TryFloat(V(key), out float v)) so.FindProperty(field).floatValue = v; }
                Int("day1_customers", "day1Customers");
                Int("day1_first_buy", "day1FirstBuy");
                Int("customer_regulars_after_day1", "regularsAfterDay1");
                Int("customer_market_size", "marketSize");
                Float("customer_ad_rate", "adRate");
                Float("customer_wom_rate", "wordOfMouth");
                Float("customer_churn", "churn");
                Float("customer_visit_prob", "visitChance");
                Int("market_day_interval", "marketDayInterval");
                Float("market_day_mult", "marketDayMultiplier");
                Int("customer_cap_per_spot", "capPerSpot");
                Float("same_day_clean_min", "sameDayCleanMin");
                Float("satisfaction_w_clean", "satisfactionClean");
                Float("satisfaction_w_tidy", "satisfactionTidy");
                Float("satisfaction_w_success", "satisfactionSuccess");
                Float("customer_random", "customerRandom");
                // 가구 자리 = 과일 진열대 자리 + 일반 진열대 자리 (진열 가구 P-4 전까지 손님 상한에만 씀)
                if (CsvValue.TryInt(V("store_spots_fruit"), out int fruitSpots) && CsvValue.TryInt(V("store_spots_shelf"), out int shelfSpots))
                    so.FindProperty("furnitureSpots").intValue = fruitSpots + shelfSpots;
                Float("disorder_buy_mult", "disorderBuyMultiplier"); // 흐트러진 칸 구매 확률 (P-3)
                // 튜토리얼 고정값 (5-7, 기획 튜토리얼 2절) — 1 · 2일차 '모두 구매'는 한 값으로 (어느 하나라도 켜져 있으면)
                Int("tutorial_day2_customers", "tutorialCustomers");
                bool hasBuy1 = CsvValue.TryBool(V("tutorial_day1_buy_all"), out bool buy1);
                bool hasBuy2 = CsvValue.TryBool(V("tutorial_day2_buy_all"), out bool buy2);
                if (hasBuy1 || hasBuy2)
                    so.FindProperty("tutorialBuyAll").boolValue = (hasBuy1 && buy1) || (hasBuy2 && buy2);
                if (CsvValue.TryBool(V("tutorial_random_off"), out bool randomOff))
                    so.FindProperty("tutorialRandomOff").boolValue = randomOff;
                Apply(so, sales, "설정 SalesSettings", report);
            }

            // 가게 정리 (P-3, 청소 v2 설정안)
            var cleaning = LoadSettings<CleaningSettingsData>("CleaningSettings");
            if (cleaning != null)
            {
                var so = new SerializedObject(cleaning);
                void Int(string key, string field) { if (CsvValue.TryInt(V(key), out int v)) so.FindProperty(field).intValue = v; }
                void Float(string key, string field) { if (CsvValue.TryFloat(V(key), out float v)) so.FindProperty(field).floatValue = v; }
                Int("trash_day1", "trashDay1");
                Int("stain_day1", "stainDay1");
                Int("trash_daily_min", "trashDailyMin");
                Int("trash_daily_max", "trashDailyMax");
                Float("stain_per_customer", "stainPerCustomer");
                Int("tutorial_day2_stains", "tutorialStains"); // 튜토리얼 고정값 (5-7)
                Float("scatter_unsold_ratio", "scatterUnsoldRatio");
                Float("scatter_per_customers", "scatterPerCustomers");
                Float("disorder_ratio", "disorderRatio");
                Float("grade_s_min", "gradeS");
                Float("grade_a_min", "gradeA");
                Float("grade_b_min", "gradeB");
                Apply(so, cleaning, "설정 CleaningSettings", report);
            }

            foreach (var key in values.Keys)
                if (!handled.Contains(key))
                    report.Waiting.Add($"{key} = {values[key]}");
        }

        private static void SetStartingItems(SerializedProperty list, string text, IdMap map, Dictionary<string, ContentData> byId, Report report, string label)
        {
            if (text == null)
                return;
            list.ClearArray();
            foreach (var part in PlanCsv.SplitList(text))
            {
                int colon = part.IndexOf(':');
                string planId = colon < 0 ? part : part.Substring(0, colon);
                int count = colon < 0 || !CsvValue.TryInt(part.Substring(colon + 1), out int c) ? 1 : Mathf.Max(1, c);
                var item = Find<ItemData>(byId, map.DevIdOf(planId));
                if (item == null)
                {
                    report.Problems.Add($"{label}: {planId}의 개발 에셋 없음 — 건너뜀");
                    continue;
                }
                list.InsertArrayElementAtIndex(list.arraySize);
                var e = list.GetArrayElementAtIndex(list.arraySize - 1);
                e.FindPropertyRelative("item").objectReferenceValue = item;
                e.FindPropertyRelative("count").intValue = count;
            }
        }

        // ── 튜토리얼 단계 (tutorial_steps.csv → Data/Tutorial/TutorialSteps, 5-7) ──

        private static void ImportTutorial(Tables t, Report report)
        {
            if (t.Tutorial == null)
            {
                report.Skipped.Add($"튜토리얼 단계 — {TutorialCsv} 없음");
                return;
            }
            int phase2 = 0;
            for (int r = 0; r < t.Tutorial.RowCount; r++)
                if (IsPhase2(t.Tutorial, r))
                    phase2++;
            report.Phase2 += phase2;
            int count = ImportTutorialSteps(t.Tutorial, report);
            if (count == 0)
                report.Problems.Add("튜토리얼 단계: 읽은 줄이 없음 (step_id 열 확인)");
        }

        /// <summary>CSV 표를 튜토리얼 데이터 에셋(없으면 만듦)의 단계 목록에 넣는다. 문구의 "—"는 글꼴용으로 바꾼다. 넣은 줄 수.</summary>
        internal static int ImportTutorialSteps(CsvTable table, Report report)
        {
            var steps = Logic.Tutorial.TutorialStep.FromCsv(table);
            var asset = LoadOrCreateTutorialAsset();
            var so = new SerializedObject(asset);
            var list = so.FindProperty("steps");
            list.arraySize = steps.Count;
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = s.Id;
                e.FindPropertyRelative("day").intValue = Mathf.Max(1, s.Day);
                e.FindPropertyRelative("phase").stringValue = s.Phase;
                e.FindPropertyRelative("text").stringValue = UiText.ForScreenFont(s.Text);
                e.FindPropertyRelative("highlight").stringValue = s.Highlight;
                e.FindPropertyRelative("completeEvent").stringValue = s.CompleteEvent;
                e.FindPropertyRelative("locks").stringValue = s.LocksText;
            }
            Apply(so, asset, $"튜토리얼 단계 {steps.Count}줄 ({asset.name})", report ?? new Report());
            return steps.Count;
        }

        /// <summary>저장소 CSV에서 튜토리얼 단계만 다시 읽는다 (Setup 메뉴). 파일이 없으면 -1.</summary>
        internal static int ImportTutorialStepsFromRepo()
        {
            string path = Path.Combine(PlanRoot, TutorialCsv);
            if (!File.Exists(path))
                return -1;
            int count = ImportTutorialSteps(CsvTable.Parse(File.ReadAllText(path, Encoding.UTF8)), new Report());
            AssetDatabase.SaveAssets();
            return count;
        }

        internal static TutorialStepsData LoadOrCreateTutorialAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TutorialStepsData>(TutorialAssetPath);
            if (asset != null)
                return asset;
            if (!AssetDatabase.IsValidFolder(TutorialFolder))
                AssetDatabase.CreateFolder(DataRoot, "Tutorial");
            asset = ScriptableObject.CreateInstance<TutorialStepsData>();
            AssetDatabase.CreateAsset(asset, TutorialAssetPath);
            return asset;
        }

        // ── 공통 ─────────────────────────────────────────

        private static T LoadSettings<T>(string name) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>($"{StatsFolder}/{name}.asset");
            if (asset == null)
                Debug.LogError($"[PlanCsv] {StatsFolder}/{name}.asset 없음 — Glow Farm/Setup/Create Placeholder Data를 먼저 실행");
            return asset;
        }

        private static string NoteOf(IdMap map, string planId)
        {
            var e = map.Find(planId);
            return e != null ? $"ID_MAP: {e.Status}" : "ID_MAP";
        }

        private static void Hide(ContentData asset, string label, Report report)
        {
            var so = new SerializedObject(asset);
            var prop = so.FindProperty("hidden");
            if (prop == null)
                return;
            prop.boolValue = true;
            if (so.ApplyModifiedPropertiesWithoutUndo())
                EditorUtility.SetDirty(asset);
            report.Hidden.Add(label);
        }

        // 목록을 비우고 다시 채우면 값이 같아도 '바뀜'으로 잡히므로, 적용 전후 직렬화 글을 비교해 실제로 바뀐 것만 센다 (멱등 확인)
        private static void Apply(SerializedObject so, UnityEngine.Object asset, string label, Report report)
        {
            string before = EditorJsonUtility.ToJson(asset);
            so.ApplyModifiedPropertiesWithoutUndo();
            if (EditorJsonUtility.ToJson(asset) == before)
                return;
            EditorUtility.SetDirty(asset);
            report.Updated.Add(label);
        }

        private static void Set(SerializedObject so, string field, int value, Report report, string planId)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
                report.Problems.Add($"{planId}: 개발 에셋에 {field} 칸이 없음");
            else
                prop.intValue = value;
        }

        private static void SetIntCol(SerializedObject so, string field, CsvTable table, int row, string column, int min, Report report, string planId)
        {
            if (CsvValue.TryInt(table.Get(row, column), out int v))
                so.FindProperty(field).intValue = Math.Max(min, v);
            else
                report.Problems.Add($"{planId}: {column} 값 '{table.Get(row, column)}'을 숫자로 읽지 못함");
        }

        private static void SetInts(SerializedProperty list, List<int> values)
        {
            list.ClearArray();
            for (int i = 0; i < values.Count; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).intValue = values[i];
            }
        }

        private static void WriteReport(Report report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 기획 CSV 가져오기 결과");
            sb.AppendLine();
            sb.AppendLine("> `Glow Farm/Data/Import Plan CSV`가 실행할 때마다 다시 쓴다. 기준: `CSV/plan/` · `ID_MAP.md`.");
            sb.AppendLine();
            sb.AppendLine("**" + report.Summary + "**");
            void Section(string title, List<string> lines)
            {
                sb.AppendLine();
                sb.AppendLine($"## {title} ({lines.Count})");
                foreach (var l in lines)
                    sb.AppendLine("- " + l);
            }
            Section("문제", report.Problems);
            Section("숨김 (값은 넣음)", report.Hidden);
            Section("건너뜀 (개발 에셋 없음)", report.Skipped);
            Section("시스템 대기 설정 (game_settings.csv)", report.Waiting);
            Section("생성", report.Created);
            Section("갱신 (값이 바뀐 에셋)", report.Updated);
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
#endif
