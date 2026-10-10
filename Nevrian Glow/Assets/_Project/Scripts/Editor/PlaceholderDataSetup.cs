#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using UnityEditor;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 기획 입력 전에 쓰는 임시 데이터 세트(BALANCE 2장 · 9장)를 만든다.
    /// 여러 번 실행해도 결과가 같다: 이미 있는 에셋은 건드리지 않고, "초기값으로 되돌리기"에서만 값을 덮어쓴다.
    /// 필드 이름 · id는 CSV 임포터(2-1)와 같다 → CSV가 오면 같은 에셋의 값만 갱신된다.
    /// 상품 · 스킬은 효과 구조를 정하는 3-3 · 4-3에서 이 메뉴에 추가한다 (결정 2026-10-08).
    /// </summary>
    public static class PlaceholderDataSetup
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string StatsFolder = DataRoot + "/Stats";
        private const string CropsFolder = DataRoot + "/Crops";
        private const string ToolsFolder = DataRoot + "/Tools";
        private const string ItemsFolder = DataRoot + "/Items";
        private const string ZonesFolder = DataRoot + "/Zones";
        private const string CustomersFolder = DataRoot + "/Customers";
        private const string CustomerPrefabPath = "Assets/_Project/Prefabs/Shop/customer.prefab";

        // BALANCE 9-5: 피피(작은) · 오르(큰). 색 · 속도 · 바구니 자리는 임시 (2026-10-11)
        private static readonly (string Id, string Name, CustomerBodySize Body, string ColorHex, float WalkMul, float Weight, int UnlockDay, Vector3 Basket)[] Customers =
        {
            ("customer_pipi", "피피", CustomerBodySize.Small, "#FF9AD5", 1.1f, 1f, 1, new Vector3(-0.24f, 0.55f, 0.1f)),
            ("customer_or", "오르", CustomerBodySize.Large, "#A08BFF", 0.85f, 1f, 1, new Vector3(-0.4f, 0.95f, 0.15f)),
        };

        private readonly struct CropRow
        {
            public readonly string Id, Name;
            public readonly int SeedPrice, SellPrice, GrowDays, WetDays, Yield, RegrowDays, MaxHarvests;
            public readonly float GiantChance, GiantYield, GiantScale;
            public readonly string SeedId, SeedName, ColorHex;

            public CropRow(string id, string name, int seedPrice, int sellPrice, int growDays, int wetDays, int yield,
                           int regrowDays, int maxHarvests, float giantChance, float giantYield, float giantScale,
                           string seedId, string seedName, string colorHex)
            {
                Id = id; Name = name; SeedPrice = seedPrice; SellPrice = sellPrice; GrowDays = growDays; WetDays = wetDays;
                Yield = yield; RegrowDays = regrowDays; MaxHarvests = maxHarvests;
                GiantChance = giantChance; GiantYield = giantYield; GiantScale = giantScale;
                SeedId = seedId; SeedName = seedName; ColorHex = colorHex;
            }
        }

        // BALANCE 2-2 (v1 2026-10-10: 버섯꽃 성장 4 → 2). 거대 작물: giantChance 0.02 / giantYieldMultiplier 2.5 / giantScale 2.2 (거대 X = 확률 0).
        // 씨앗 id · 그레이박스 색은 BALANCE 2-12 · 9-5 (월광 시안 · 버섯꽃 보라 · 등불 금빛).
        private static readonly CropRow[] Crops =
        {
            new CropRow("crop_moonroot",       "월광 뿌리", 10, 15, 1, 1, 1, 0, 1, 0.02f, 2.5f, 2.2f, "item_seed_moonroot",       "월광 뿌리 씨앗", "#62F2E1"),
            new CropRow("crop_mushroomflower", "버섯꽃",    20, 40, 2, 2, 2, 0, 1, 0.02f, 2.5f, 2.2f, "item_seed_mushroomflower", "버섯꽃 씨앗",    "#A08BFF"),
            new CropRow("crop_lanternfruit",   "등불 열매", 70, 80, 8, 1, 2, 4, 3, 0f,    1f,   1f,   "item_seed_lanternfruit",   "등불 열매 씨앗", "#FFD27A"),
        };

        private const int ItemStackSize = 10; // BALANCE 2-4: 도구 · 장치 외 칸당 10개

        // BALANCE 2-11 도구 등급 (v1 2026-10-10). 등급 하나 = 에셋 하나.
        private static readonly (string Id, string Name, float WorkPerUse)[] Hoes =
        {
            ("tool_hoe_1", "낡은 괭이", 1f),
            ("tool_hoe_2", "철 괭이", 1.5f),
            ("tool_hoe_3", "발광 괭이", 3f),
        };

        private const float WateringUseDelay = 0.3f; // BALANCE 2-11: 기울이기 0.3초 뒤 물 (괭이 · 대걸레는 0)

        private static readonly (string Id, string Name, int CellCount)[] WateringCans =
        {
            ("tool_watering_1", "물뿌리개", 1),
            ("tool_watering_2", "물뿌리개 2등급", 2),
            ("tool_watering_3", "물뿌리개 3등급", 4),
            ("tool_watering_4", "물뿌리개 4등급", 8),
        };

        private static readonly (string Id, string Name, float Radius, float Speed)[] Mops =
        {
            ("tool_mop_1", "낡은 대걸레", 0.9f, 0.9f),
            ("tool_mop_2", "좋은 대걸레", 1.3f, 1.2f),
            ("tool_mop_3", "발광 대걸레", 1.7f, 1.5f),
        };

        // BALANCE 2-3 시작 핫바 (임시, GDD 열린 항목 22): 1번 칸부터, 4번은 수확물 자리로 비워 둔다.
        // 대걸레는 가게 창고에 둔다(2-2, 시뮬레이션 추천안). 월광 씨앗 10개는 상점(3-3)이 생기기 전 개발용 임시.
        private static readonly (string Id, int Count)[] StartingHotbar =
        {
            ("tool_hoe_1", 1),
            ("tool_watering_1", 1),
            ("item_seed_moonroot", 10),
        };

        // BALANCE 2-3 시작 창고: 바로 팔 수 있는 시작 작물 2개 + 가게에서 쓸 대걸레 (1-4 결정)
        private static readonly (string Id, int Count)[] StartingStorage =
        {
            ("crop_moonroot", 2),
            ("tool_mop_1", 1),
        };

        private class Report
        {
            public int Created, Reset, Kept, Failed;
            public override string ToString() => $"생성 {Created} · 초기화 {Reset} · 유지 {Kept} · 실패 {Failed}";
        }

        [MenuItem("Glow Farm/Setup/Create Placeholder Data")]
        private static void CreateMenu()
        {
            Debug.Log("[Setup] 임시 데이터 세트: " + Run(false));
        }

        [MenuItem("Glow Farm/Setup/Reset Placeholder Data to Defaults")]
        private static void ResetMenu()
        {
            if (!EditorUtility.DisplayDialog("초기값으로 되돌리기",
                    "임시 데이터 에셋의 값을 BALANCE 초기값으로 덮어쓴다. 직접 바꾼 값과 CSV로 가져온 값이 사라진다.",
                    "되돌리기", "취소"))
                return;
            Debug.Log("[Setup] 임시 데이터 초기화: " + Run(true));
        }

        /// <summary>임시 데이터 세트를 만든다. resetExisting이면 이미 있는 에셋도 초기값으로 덮어쓴다.</summary>
        public static string Run(bool resetExisting)
        {
            var report = new Report();

            var crops = new CropData[Crops.Length];
            for (int i = 0; i < Crops.Length; i++)
            {
                var row = Crops[i];
                crops[i] = Ensure<CropData>($"{CropsFolder}/{row.Id}.asset", resetExisting, report, so =>
                {
                    so.FindProperty("id").stringValue = row.Id;
                    so.FindProperty("displayName").stringValue = row.Name;
                    so.FindProperty("seedPrice").intValue = row.SeedPrice;
                    so.FindProperty("sellPrice").intValue = row.SellPrice;
                    so.FindProperty("growDays").intValue = row.GrowDays;
                    so.FindProperty("wetDays").intValue = row.WetDays;
                    so.FindProperty("harvestYield").intValue = row.Yield;
                    so.FindProperty("regrowDays").intValue = row.RegrowDays;
                    so.FindProperty("maxHarvests").intValue = row.MaxHarvests;
                    so.FindProperty("giantChance").floatValue = row.GiantChance;
                    so.FindProperty("giantYieldMultiplier").floatValue = row.GiantYield;
                    so.FindProperty("giantScale").floatValue = row.GiantScale;
                    so.FindProperty("maxStack").intValue = ItemStackSize;
                    if (ColorUtility.TryParseHtmlString(row.ColorHex, out var color))
                        so.FindProperty("greyboxColor").colorValue = color;
                });
            }

            var items = new List<ItemData>();
            for (int i = 0; i < Crops.Length; i++)
            {
                var row = Crops[i];
                var crop = crops[i];
                items.Add(Ensure<SeedData>($"{ItemsFolder}/{row.SeedId}.asset", resetExisting, report, so =>
                {
                    SetIdentity(so, row.SeedId, row.SeedName);
                    so.FindProperty("maxStack").intValue = ItemStackSize;
                    so.FindProperty("crop").objectReferenceValue = crop;
                }));
            }
            foreach (var hoe in Hoes)
            {
                items.Add(Ensure<HoeData>($"{ToolsFolder}/{hoe.Id}.asset", resetExisting, report, so =>
                {
                    SetIdentity(so, hoe.Id, hoe.Name);
                    so.FindProperty("workPerUse").floatValue = hoe.WorkPerUse;
                }));
            }
            foreach (var can in WateringCans)
            {
                items.Add(Ensure<WateringCanData>($"{ToolsFolder}/{can.Id}.asset", resetExisting, report, so =>
                {
                    SetIdentity(so, can.Id, can.Name);
                    so.FindProperty("cellCount").intValue = can.CellCount;
                    so.FindProperty("useDelay").floatValue = WateringUseDelay;
                }));
            }
            foreach (var mop in Mops)
            {
                items.Add(Ensure<MopData>($"{ToolsFolder}/{mop.Id}.asset", resetExisting, report, so =>
                {
                    SetIdentity(so, mop.Id, mop.Name);
                    so.FindProperty("radius").floatValue = mop.Radius;
                    so.FindProperty("speed").floatValue = mop.Speed;
                }));
            }

            // BALANCE 2-10 지역 (임시 분위기 — 5-1 조명 · 후처리에서 확정). 농장 = 농사 단계 · 하루 시작, 가게 = 청소·진열 · 장사 · 정산.
            Ensure<ZoneProfileData>($"{ZonesFolder}/zone_farm.asset", resetExisting, report, so =>
            {
                SetIdentity(so, "zone_farm", "농장");
                SetPhases(so.FindProperty("phases"), DayPhase.Farming);
                so.FindProperty("isDayStart").boolValue = true;
                so.FindProperty("ambientColor").colorValue = new Color(0.20f, 0.22f, 0.40f);  // 차가운 청보라 황혼
                so.FindProperty("fog").boolValue = true;
                so.FindProperty("fogColor").colorValue = new Color(0.055f, 0.15f, 0.25f);     // 심청 #0E2640
                so.FindProperty("fogDensity").floatValue = 0.02f;
                so.FindProperty("bgmId").stringValue = "bgm_twilight_field";
            });
            Ensure<ZoneProfileData>($"{ZonesFolder}/zone_shop.asset", resetExisting, report, so =>
            {
                SetIdentity(so, "zone_shop", "가게");
                SetPhases(so.FindProperty("phases"), DayPhase.CleanDisplay, DayPhase.Sales, DayPhase.Settlement);
                so.FindProperty("isDayStart").boolValue = false;
                so.FindProperty("ambientColor").colorValue = new Color(0.36f, 0.29f, 0.36f);  // 한 단계 따뜻하게 (GDD 8)
                so.FindProperty("fog").boolValue = false;
                so.FindProperty("fogColor").colorValue = new Color(0.16f, 0.14f, 0.25f);
                so.FindProperty("fogDensity").floatValue = 0f;
                so.FindProperty("bgmId").stringValue = "bgm_lumian_market";
            });

            // BALANCE 2-1
            Ensure<PlayerSettingsData>($"{StatsFolder}/PlayerSettings.asset", resetExisting, report, so =>
            {
                so.FindProperty("moveSpeed").floatValue = 4.6f;
                so.FindProperty("sprintMultiplier").floatValue = 1.6f;
                so.FindProperty("lookSensitivity").floatValue = 0.14f;
                so.FindProperty("pitchLimit").floatValue = 83f;
                so.FindProperty("eyeHeight").floatValue = 1.7f;
                so.FindProperty("headBobAmplitude").floatValue = 0.008f;
                so.FindProperty("headBobFrequency").floatValue = 0.8f;
                so.FindProperty("interactMaxDistance").floatValue = 5f;
            });

            // BALANCE 2-3 (v1 2026-10-10: 시작 자금 100 → 150)
            Ensure<GameStartSettingsData>($"{StatsFolder}/GameStartSettings.asset", resetExisting, report, so =>
            {
                so.FindProperty("startingMoney").intValue = 150;
                var known = new List<ItemData>(items);
                known.AddRange(crops);
                SetStartingItems(so.FindProperty("startingHotbarItems"), StartingHotbar, known, "시작 핫바");
                SetStartingItems(so.FindProperty("startingStorageItems"), StartingStorage, known, "시작 창고");
                so.FindProperty("tillWorkRequired").floatValue = 3f; // BALANCE 2-11 칸 필요 작업량
                so.FindProperty("startPlotCount").intValue = 2;
                so.FindProperty("maxPlotCount").intValue = 5;
                so.FindProperty("cellsPerPlot").intValue = 16;
                so.FindProperty("cellPitch").floatValue = 2f;
                so.FindProperty("bedPitch").floatValue = 5.6f;
                so.FindProperty("plotPitch").floatValue = 11.2f;
                so.FindProperty("plotExpandDirection").vector3Value = new Vector3(-1f, 0f, 0f);
            });

            // BALANCE 2-4
            Ensure<InventorySettingsData>($"{StatsFolder}/InventorySettings.asset", resetExisting, report, so =>
            {
                so.FindProperty("hotbarBaseSlots").intValue = 4;
                so.FindProperty("hotbarMaxSlots").intValue = 8;
                so.FindProperty("bagBaseSlots").intValue = 4;      // v1 2026-10-08: 가방 4 → 32칸, 7단계 × 4
                so.FindProperty("bagSlotsPerExpand").intValue = 4;
                so.FindProperty("bagMaxSlots").intValue = 32;
                so.FindProperty("storageAccessRadius").floatValue = 2f;
                so.FindProperty("shelfBaseSlots").intValue = 6;
                so.FindProperty("shelfItemsPerSlot").intValue = 4;
                so.FindProperty("shelfSlotsPerUpgrade").intValue = 2;
                so.FindProperty("shelfSnapDistance").floatValue = 0.6f;
                so.FindProperty("placeDuration").floatValue = 0.15f;
            });

            // BALANCE 2-12 쓰레기 아이템 (2-4): 칸당 10개, 창고 불가
            var trashItem = Ensure<ItemData>($"{ItemsFolder}/item_trash.asset", resetExisting, report, so =>
            {
                SetIdentity(so, "item_trash", "쓰레기");
                so.FindProperty("maxStack").intValue = ItemStackSize;
                so.FindProperty("canStore").boolValue = false;
            });

            // BALANCE 2-5
            Ensure<CleaningSettingsData>($"{StatsFolder}/CleaningSettings.asset", resetExisting, report, so =>
            {
                SetDailyCount(so.FindProperty("trash"), 6, 1, 6);
                SetDailyCount(so.FindProperty("stain"), 4, 2, 4);
                so.FindProperty("minSpacing").floatValue = 0.8f;
                so.FindProperty("placementAttempts").intValue = 30;
                so.FindProperty("trashItem").objectReferenceValue = trashItem;
            });

            // BALANCE 2-6
            Ensure<SalesSettingsData>($"{StatsFolder}/SalesSettings.asset", resetExisting, report, so =>
            {
                so.FindProperty("baseCustomers").intValue = 8;
                so.FindProperty("customerDayDivisor").intValue = 2;
                so.FindProperty("customerCap").intValue = 24;
                so.FindProperty("dirtyCustomerMultiplier").floatValue = 0.6f;
                so.FindProperty("salesDuration").floatValue = 30f;
                so.FindProperty("spawnWindow").floatValue = 0.8f;
                so.FindProperty("maxConcurrentCustomers").intValue = 6;
                so.FindProperty("walkSpeed").floatValue = 2.1f;
                so.FindProperty("browseDuration").floatValue = 1.2f;
                so.FindProperty("checkoutDuration").floatValue = 1f;
                so.FindProperty("purchaseMin").intValue = 1;
                so.FindProperty("purchaseMax").intValue = 3;
                so.FindProperty("stopDistance").floatValue = 0.3f;
                so.FindProperty("queueSpacing").floatValue = 0.8f;
            });

            // BALANCE 9-5 임시 손님 2종 (3-2, 2026-10-11) — 프리팹은 Glow Farm/Setup/Create Customer Greybox
            var customerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CustomerPrefabPath);
            foreach (var c in Customers)
            {
                Ensure<CustomerData>($"{CustomersFolder}/{c.Id}.asset", resetExisting, report, so =>
                {
                    SetIdentity(so, c.Id, c.Name);
                    so.FindProperty("prefab").objectReferenceValue = customerPrefab;
                    so.FindProperty("bodySize").enumValueIndex = (int)c.Body;
                    if (ColorUtility.TryParseHtmlString(c.ColorHex, out var color))
                        so.FindProperty("greyboxColor").colorValue = color;
                    so.FindProperty("basketSocketOffset").vector3Value = c.Basket;
                    so.FindProperty("itemSocketOffset").vector3Value = c.Basket + new Vector3(0.1f, 0.1f, 0.1f);
                    so.FindProperty("walkSpeedMul").floatValue = c.WalkMul;
                    so.FindProperty("spawnWeight").floatValue = c.Weight;
                    so.FindProperty("unlockDay").intValue = c.UnlockDay;
                });
            }

            // BALANCE 2-7 (v1 2026-10-10: 80 + 40 × (레벨 − 1) = 40 + 40 × 레벨 → base 100 → 40)
            Ensure<ProgressionData>($"{StatsFolder}/Progression.asset", resetExisting, report, so =>
            {
                so.FindProperty("expPerRevenue").floatValue = 1f;
                so.FindProperty("levelUpBase").intValue = 40;
                so.FindProperty("levelUpPerLevel").intValue = 40;
                so.FindProperty("skillPointsPerLevel").intValue = 3;
            });

            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        private static void SetPhases(SerializedProperty list, params DayPhase[] phases)
        {
            list.ClearArray();
            foreach (var phase in phases)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).enumValueIndex = (int)phase;
            }
        }

        private static void SetStartingItems(SerializedProperty list, (string Id, int Count)[] rows, List<ItemData> known, string label)
        {
            list.ClearArray();
            foreach (var (itemId, count) in rows)
            {
                var item = known.Find(t => t != null && t.Id == itemId);
                if (item == null)
                {
                    Debug.LogError($"[Setup] {label}의 {itemId}를 찾지 못함 — 건너뜀");
                    continue;
                }
                list.InsertArrayElementAtIndex(list.arraySize);
                var element = list.GetArrayElementAtIndex(list.arraySize - 1);
                element.FindPropertyRelative("item").objectReferenceValue = item;
                element.FindPropertyRelative("count").intValue = count;
            }
        }

        private static void SetIdentity(SerializedObject so, string id, string displayName)
        {
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
        }

        private static void SetDailyCount(SerializedProperty prop, int baseCount, int dayDivisor, int dayBonusCap)
        {
            prop.FindPropertyRelative("baseCount").intValue = baseCount;
            prop.FindPropertyRelative("dayDivisor").intValue = dayDivisor;
            prop.FindPropertyRelative("dayBonusCap").intValue = dayBonusCap;
        }

        private static T Ensure<T>(string path, bool resetExisting, Report report, Action<SerializedObject> apply)
            where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    Debug.LogError($"[Setup] {path}에 다른 종류의 에셋이 있어 건너뜀 — 확인 후 다시 실행");
                    report.Failed++;
                    return null;
                }

                // 값을 먼저 채운 뒤 에셋으로 만든다 — 만드는 순간 ContentDatabase 자동 검사가 돌아 빈 id 오류를 내지 않게.
                EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                asset = ScriptableObject.CreateInstance<T>();
                var fresh = new SerializedObject(asset);
                apply(fresh);
                fresh.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, path);
                report.Created++;
                return asset;
            }

            if (!resetExisting)
            {
                report.Kept++;
                return asset;
            }

            var so = new SerializedObject(asset);
            apply(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            report.Reset++;
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
#endif
