using System.Collections.Generic;
using System.Linq;
using NevrianGlowFarm.Logic.Farm;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Sales;
using NevrianGlowFarm.Logic.Shop;
using NevrianGlowFarm.Logic.Simulation;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>4-1 측정 봇: 상점 · 농사 · 장사 · 정산 · 스킬 순서, 같은 시드 = 같은 결과, 목표 판정, 리포트.</summary>
    public class DaySimulatorTests
    {
        private static ProductSpec Seed(string id, int price, int stock, string skill = null) =>
            new ProductSpec(id, new[] { new TierSpec(price, skill == null ? null : new[] { new UnlockRequirement(UnlockKind.SkillLevel, skill, 1) }) }, true, new[] { stock });

        // 월광(1일 · 15달러) + 스킬로 여는 버섯꽃(2일 · 40달러 × 2) — BALANCE 2-2 값
        private static DaySimConfig Config(int money = 100, int plots = 1)
        {
            return new DaySimConfig
            {
                Days = 3,
                StartMoney = money,
                CellsPerPlot = 16,
                StartPlots = plots,
                ShelfSlots = 6,
                ShelfItemsPerSlot = 4,
                Sales = new SalesSpec(8, 2, 24, 0.6, 30, 0.8, 6, 1, 3),
                Curve = new LevelCurve(1, 40, 40, 3),
                Cleanliness = 1,
                Crops = new List<SimCrop>
                {
                    new SimCrop { Id = "crop_a", Spec = new CropSpec(1, 1, 1, 0, 1), SellPrice = 15, SeedProduct = Seed("shop_a", 10, 12) },
                    new SimCrop { Id = "crop_b", Spec = new CropSpec(2, 2, 2, 0, 1), SellPrice = 40, SeedProduct = Seed("shop_b", 20, 8, "skill_b") },
                },
                PlotProduct = new ProductSpec("shop_plot", new[] { new TierSpec(250), new TierSpec(600) }, false),
            };
        }

        [Test]
        public void Day1_BuysWhatMoneyAllows_PlantsAndWaters()
        {
            var run = DaySimulator.Run(Config(), 1);
            var d1 = run.Days[0];
            Assert.AreEqual(10, d1.Planted["crop_a"], "100달러 ÷ 10 = 10개 (입고 12 · 빈 칸 16 안)");
            Assert.AreEqual(100, d1.Spent);
            Assert.AreEqual(0, d1.Revenue, "1일차는 팔 것이 없음");
            Assert.AreEqual(8, d1.Customers);
            Assert.AreEqual(0, d1.Money);
            Assert.IsFalse(d1.Unlocked.Contains("crop_b"), "스킬 전에는 버섯꽃이 안 보임");

            var d2 = run.Days[1];
            Assert.AreEqual(10, d2.Harvested["crop_a"], "물 준 월광은 다음 날 수확");
            Assert.AreEqual(d2.ItemsSold * 15, d2.Revenue);
            Assert.AreEqual(10 - d2.ItemsSold, d2.Unsold, "못 판 것은 진열대에 남음");
            Assert.AreEqual(d2.Revenue - d2.Spent, d2.Money);
        }

        [Test]
        public void StartSeedsAndCrops_UsedFirst()
        {
            var config = Config(money: 0);
            config.StartSeeds["crop_a"] = 3;
            config.StartCrops["crop_a"] = 2;
            var d1 = DaySimulator.Run(config, 1).Days[0];
            Assert.AreEqual(3, d1.Planted["crop_a"]);
            Assert.AreEqual(0, d1.Spent);
            Assert.AreEqual(30, d1.Revenue, "시작 수확물 2개 판매 (손님 8명이면 다 팔림)");
        }

        [Test]
        public void SameSeed_SameResult_DifferentSeed_MayDiffer()
        {
            var a = DaySimulator.Run(Config(), 7);
            var b = DaySimulator.Run(Config(), 7);
            CollectionAssert.AreEqual(a.Days.Select(d => d.Revenue), b.Days.Select(d => d.Revenue));
            CollectionAssert.AreEqual(a.Days.Select(d => d.Money), b.Days.Select(d => d.Money));
        }

        [Test]
        public void PlotBoughtFirst_WhenAffordable()
        {
            var run = DaySimulator.Run(Config(money: 300), 1);
            var d1 = run.Days[0];
            Assert.AreEqual(32, d1.OpenCells, "250달러 구획 먼저 → 16 + 16칸");
            Assert.AreEqual(5, d1.Planted["crop_a"], "남은 50달러로 5개");
            Assert.AreEqual(300, d1.Spent);
        }

        [Test]
        public void Skills_InPriority_SavesForExpensiveFirst()
        {
            var config = Config(money: 0);
            config.StartCrops["crop_a"] = 6; // 1일차 매출 90 → Lv2 (SP 3)
            config.SkillPriority.Add(new SimSkill { Id = "skill_expensive", Costs = new[] { 6 } });
            config.SkillPriority.Add(new SimSkill { Id = "skill_b", Costs = new[] { 3 } });
            var d1 = DaySimulator.Run(config, 1).Days[0];
            Assert.AreEqual(2, d1.Level);
            Assert.AreEqual(3, d1.SkillPoints, "앞 스킬(6 SP)을 위해 아껴 둠");
            Assert.IsEmpty(d1.SkillsLearned);

            config.SkillPriority.RemoveAt(0);
            var run = DaySimulator.Run(config, 1);
            CollectionAssert.AreEqual(new[] { "skill_b 1" }, run.Days[0].SkillsLearned);
            Assert.IsTrue(run.Days[1].Unlocked.Contains("crop_b"), "배운 다음 날 상점에 버섯꽃");
        }

        [Test]
        public void Prerequisite_Blocks()
        {
            var config = Config(money: 0);
            config.StartCrops["crop_a"] = 6;
            config.SkillPriority.Add(new SimSkill { Id = "skill_late", Costs = new[] { 1 }, PrerequisiteId = "skill_none", PrerequisiteLevel = 1 });
            var d1 = DaySimulator.Run(config, 1).Days[0];
            Assert.IsEmpty(d1.SkillsLearned);
            Assert.AreEqual(3, d1.SkillPoints);
        }

        [Test]
        public void Goals_JudgedOnTheirDay()
        {
            var config = Config();
            config.Goals.Add(new SimGoal { Day = 1, Kind = SimGoalKind.PlantedAtLeast, Amount = 4, Label = "4칸" });
            config.Goals.Add(new SimGoal { Day = 1, Kind = SimGoalKind.PlantedAtLeast, CropId = "crop_a", Amount = 11, Label = "11칸" });
            config.Goals.Add(new SimGoal { Day = 2, Kind = SimGoalKind.HarvestedAtLeast, CropId = "crop_a", Amount = 1, Label = "수확" });
            config.Goals.Add(new SimGoal { Day = 2, Kind = SimGoalKind.CropUnlocked, CropId = "crop_b", Label = "해금" });
            config.Goals.Add(new SimGoal { Day = 2, Kind = SimGoalKind.PlantedMoreThanPreviousDay, CropId = "crop_a", Label = "더" });
            var run = DaySimulator.Run(config, 1);
            var d1 = run.Days[0].Goals.Select(g => g.Value).ToArray();
            CollectionAssert.AreEqual(new[] { true, false }, d1);
            var d2 = run.Days[1].Goals;
            Assert.AreEqual(3, d2.Count);
            Assert.IsTrue(d2[0].Value);
            Assert.IsFalse(d2[1].Value, "스킬 없음 → 해금 안 됨");
            Assert.IsFalse(run.Days[1].AllGoalsMet);
            Assert.IsEmpty(run.Days[2].Goals);
            Assert.AreEqual(0, run.GoalDaysMet);
        }

        [Test]
        public void FreeCells_CountsReadyCrops()
        {
            var crops = new Dictionary<string, SimCrop> { { "crop_a", new SimCrop { Id = "crop_a", Spec = new CropSpec(1, 1, 1, 0, 1) } } };
            var farm = new FarmState(4, 1, id => crops[id].Spec);
            FarmRules.TryTill(farm.Cells[0], 1, 0);
            FarmRules.TryPlant(farm.Cells[0], "crop_a");
            FarmRules.TryWater(farm.Cells[0], crops["crop_a"].Spec);
            Assert.AreEqual(3, DaySimulator.FreeCellsToday(farm, crops));
            new NightPipeline().Run(farm);
            Assert.AreEqual(4, DaySimulator.FreeCellsToday(farm, crops), "다 자란 칸은 오늘 수확하면 빔");
        }

        [Test]
        public void Report_HasSectionsAndSummary()
        {
            var config = Config();
            config.Goals.Add(new SimGoal { Day = 1, Kind = SimGoalKind.PlantedAtLeast, Amount = 4, Label = "밭 4칸" });
            var runs = new List<SimRun> { DaySimulator.Run(config, 1), DaySimulator.Run(config, 2) };
            string md = DaySimReport.Markdown(config, runs, id => id == "crop_a" ? "월광" : null);
            StringAssert.Contains("# 측정 봇 리포트 (1~3일차 × 시드 2)", md);
            StringAssert.Contains("## 일별", md);
            StringAssert.Contains("| 1 | 밭 4칸 | 모든 작물 새로 심은 칸 ≥ 4 | 2 / 2 |", md);
            StringAssert.Contains("월광 10", md);
            var s = DaySimReport.Summarize(runs);
            Assert.AreEqual(1, s.GoalDays);
            Assert.AreEqual(1, s.GoalDaysMet);
        }
    }
}
