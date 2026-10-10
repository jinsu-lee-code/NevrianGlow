using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Simulation;
using UnityEditor;
using UnityEngine;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 측정 봇 실행 (TASKS 4-1). 데이터 에셋 → Logic DaySimConfig로 옮겨 시드마다 DaySimulator를 돌리고,
    /// 저장소 Reports/daysim_날짜_시각.md 에 리포트를 쓴다. 게임 코드와 같은 규칙이라 수치를 바꾸면 바로 다시 돌려 비교한다.
    /// </summary>
    public static class DaySimulatorRunner
    {
        private const string SettingsPath = "Assets/_Project/Data/Stats/DaySimulatorSettings.asset";

        [MenuItem("Glow Farm/측정/측정 봇 실행")]
        public static void RunFromMenu()
        {
            string path = Run(out string summary);
            if (path != null)
                Debug.Log($"[측정 봇] {summary}\n리포트: {path}");
        }

        /// <summary>설정 에셋으로 돌리고 리포트 파일 경로를 돌려준다 (실패하면 null).</summary>
        public static string Run(out string summary)
        {
            summary = null;
            var settings = AssetDatabase.LoadAssetAtPath<DaySimulatorSettingsData>(SettingsPath);
            if (settings == null)
            {
                Debug.LogError($"[측정 봇] {SettingsPath} 없음 - Glow Farm/Setup/Create Placeholder Data를 먼저 실행");
                return null;
            }
            var config = BuildConfig(settings, out var notes);
            if (config == null)
                return null;

            var runs = new List<SimRun>(settings.SeedCount);
            for (int i = 0; i < settings.SeedCount; i++)
                runs.Add(DaySimulator.Run(config, settings.BaseSeed + i));

            var db = settings.Database;
            string NameOf(string id)
            {
                var crop = db.Get<CropData>(id);
                return crop != null ? crop.DisplayName : id;
            }
            string report = DaySimReport.Markdown(config, runs, NameOf, notes);

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Reports"));
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, $"daysim_{DateTime.Now:yyyyMMdd_HHmm}.md");
            File.WriteAllText(file, report, new System.Text.UTF8Encoding(false));

            var s = DaySimReport.Summarize(runs);
            summary = $"{config.Days}일차 평균: 레벨 {s.Level:0.#} · SP {s.SkillPoints:0.#} · 보유 {s.Money:0}달러 · 목표 달성 일차 {s.GoalDaysMet:0.#} / {s.GoalDays}";
            return file;
        }

        /// <summary>데이터 에셋 → 봇 조건. 콘텐츠는 종류(타입)로 찾는다 — id로 분기하지 않는다.</summary>
        public static DaySimConfig BuildConfig(DaySimulatorSettingsData settings, out List<string> notes)
        {
            notes = new List<string>();
            if (settings.GameStart == null || settings.Sales == null || settings.Progression == null || settings.Inventory == null || settings.Database == null)
            {
                Debug.LogError("[측정 봇] DaySimulatorSettings의 참조가 비어 있음 - 인스펙터 확인", settings);
                return null;
            }
            var start = settings.GameStart;
            var db = settings.Database;
            var config = new DaySimConfig
            {
                Days = settings.Days,
                StartMoney = start.StartingMoney,
                CellsPerPlot = start.CellsPerPlot,
                StartPlots = start.StartPlotCount,
                ShelfSlots = settings.Inventory.ShelfBaseSlots,
                ShelfItemsPerSlot = settings.Inventory.ShelfItemsPerSlot,
                Sales = settings.Sales.ToSpec(),
                Curve = settings.Progression.ToCurve(),
                Cleanliness = settings.Cleanliness,
                CustomerBonus = settings.CustomerBonus,
            };

            // 시작 소지품: 씨앗은 심을 씨앗, 작물은 바로 팔 수확물 (핫바 · 창고 모두)
            foreach (var row in start.StartingHotbar.Concat(start.StartingStorage))
            {
                if (row.Item is SeedData seed && seed.Crop != null)
                    Add(config.StartSeeds, seed.Crop.Id, row.Count);
                else if (row.Item is CropData crop)
                    Add(config.StartCrops, crop.Id, row.Count);
            }

            var itemProducts = db.All<ItemProductData>().ToList();
            foreach (var crop in db.All<CropData>())
            {
                var sim = new SimCrop { Id = crop.Id, Spec = crop.ToSpec(), SellPrice = crop.SellPrice };
                var product = itemProducts.FirstOrDefault(p => p.Item is SeedData s && s.Crop == crop);
                if (product != null)
                {
                    sim.SeedProduct = product.ToSpec();
                    sim.SeedsPerBuy = product.Count;
                }
                else
                    notes.Add($"{crop.DisplayName}: 씨앗 상품 없음 - 봇이 사지 않음");
                config.Crops.Add(sim);
            }

            var plot = db.All<PlotExpandProductData>().FirstOrDefault();
            if (plot != null)
            {
                config.PlotProduct = plot.ToSpec();
                config.PlotsPerTier = plot.Plots;
            }

            foreach (var skill in settings.SkillPriority)
                config.SkillPriority.Add(skill.ToLogic());
            foreach (var goal in settings.Goals)
                config.Goals.Add(goal.ToLogic());

            notes.Add("봇 가정: 시간 · 이동 · 핫바/가방 칸 수는 보지 않음(창고를 오가며 다 처리), 도구 등급 · 가방 · 진열대 확장은 사지 않음");
            notes.Add("스킬은 단계만 배움 - 효과(고품질 판매가 배율 등)는 StatSystem(4-3) 전이라 반영 안 됨, 비용은 BALANCE 9-3 임시");
            return config;
        }

        private static void Add(Dictionary<string, int> counts, string id, int n) =>
            counts[id] = counts.TryGetValue(id, out int c) ? c + n : n;
    }
}
