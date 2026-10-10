#if UNITY_EDITOR
using System;
using System.IO;
using NevrianGlowFarm.Data;
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

        private readonly struct CropRow
        {
            public readonly string Id, Name;
            public readonly int SeedPrice, SellPrice, GrowDays, WetDays, Yield, RegrowDays, MaxHarvests;
            public readonly float GiantChance, GiantYield, GiantScale;

            public CropRow(string id, string name, int seedPrice, int sellPrice, int growDays, int wetDays, int yield,
                           int regrowDays, int maxHarvests, float giantChance, float giantYield, float giantScale)
            {
                Id = id; Name = name; SeedPrice = seedPrice; SellPrice = sellPrice; GrowDays = growDays; WetDays = wetDays;
                Yield = yield; RegrowDays = regrowDays; MaxHarvests = maxHarvests;
                GiantChance = giantChance; GiantYield = giantYield; GiantScale = giantScale;
            }
        }

        // BALANCE 2-2 (v1 2026-10-10: 버섯꽃 성장 4 → 2). 거대 작물: giantChance 0.02 / giantYieldMultiplier 2.5 / giantScale 2.2 (거대 X = 확률 0).
        private static readonly CropRow[] Crops =
        {
            new CropRow("crop_moonroot",       "월광 뿌리", 10, 15, 1, 1, 1, 0, 1, 0.02f, 2.5f, 2.2f),
            new CropRow("crop_mushroomflower", "버섯꽃",    20, 40, 2, 2, 2, 0, 1, 0.02f, 2.5f, 2.2f),
            new CropRow("crop_lanternfruit",   "등불 열매", 70, 80, 8, 1, 2, 4, 3, 0f,    1f,   1f),
        };

        // BALANCE 9-4 핫바 1~5 순서. 핫바에 놓는 순서는 씬의 Hotbar 인스펙터가 정한다.
        private static readonly (string Id, string Name)[] Tools =
        {
            ("tool_hand", "손"),
            ("tool_seedbag", "씨앗 주머니"),
            ("tool_wateringcan", "물뿌리개"),
            ("tool_mop", "대걸레"),
            ("tool_fertilizer", "비료"),
        };

        private const string StartingCropId = "crop_moonroot"; // BALANCE 2-3 시작 작물
        private const int StartingCropCount = 2;

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
                });
            }

            foreach (var tool in Tools)
            {
                Ensure<ToolData>($"{ToolsFolder}/{tool.Id}.asset", resetExisting, report, so =>
                {
                    so.FindProperty("id").stringValue = tool.Id;
                    so.FindProperty("displayName").stringValue = tool.Name;
                });
            }

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
                var list = so.FindProperty("startingCrops");
                list.ClearArray();
                var startCrop = Array.Find(crops, c => c != null && c.Id == StartingCropId);
                if (startCrop != null)
                {
                    list.InsertArrayElementAtIndex(0);
                    var element = list.GetArrayElementAtIndex(0);
                    element.FindPropertyRelative("crop").objectReferenceValue = startCrop;
                    element.FindPropertyRelative("count").intValue = StartingCropCount;
                }
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
                so.FindProperty("baseSlots").intValue = 4;
                so.FindProperty("slotsPerExpand").intValue = 4;
                so.FindProperty("maxSlots").intValue = 20;
                so.FindProperty("stackSize").intValue = 10;
                so.FindProperty("storageAccessRadius").floatValue = 2f;
                so.FindProperty("shelfBaseSlots").intValue = 6;
                so.FindProperty("shelfItemsPerSlot").intValue = 4;
                so.FindProperty("shelfSlotsPerUpgrade").intValue = 2;
                so.FindProperty("shelfSnapDistance").floatValue = 0.6f;
                so.FindProperty("placeDuration").floatValue = 0.15f;
            });

            // BALANCE 2-5
            Ensure<CleaningSettingsData>($"{StatsFolder}/CleaningSettings.asset", resetExisting, report, so =>
            {
                SetDailyCount(so.FindProperty("trash"), 6, 1, 6);
                SetDailyCount(so.FindProperty("stain"), 4, 2, 4);
                so.FindProperty("minSpacing").floatValue = 0.8f;
                so.FindProperty("placementAttempts").intValue = 30;
                so.FindProperty("mopRadius").floatValue = 0.9f;
                so.FindProperty("mopSpeed").floatValue = 0.9f;
                so.FindProperty("carryCount").intValue = 1;
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
            bool created = false;

            if (asset == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    Debug.LogError($"[Setup] {path}에 다른 종류의 에셋이 있어 건너뜀 — 확인 후 다시 실행");
                    report.Failed++;
                    return null;
                }

                EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                created = true;
            }

            if (!created && !resetExisting)
            {
                report.Kept++;
                return asset;
            }

            var so = new SerializedObject(asset);
            apply(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            if (created) report.Created++;
            else report.Reset++;
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
