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
                Sales = new SalesSpec(30, 0.8, 6, 1, 3),
                // 1일차 8명 고정(첫 손님 무작위) · 단골 16명에서 시작 · 무작위 폭 0 — 봇 규칙 검사용 (손님 모델 자체는 CustomerModelTests)
                CustomerModel = new CustomerModelSpec(8, 0, 16, 40, 0.35, 0.1, 0.5, 7, 1.5, 3, 0.8, 0.35, 0.35, 0.3, 0),
                FurnitureSpots = 8,
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
        public void SeedsFirst_PlotWithLeftover()
        {
            var config = Config(money: 300);
            config.PlotBeforeSeeds = false;
            var d1 = DaySimulator.Run(config, 1).Days[0];
            Assert.AreEqual(12, d1.Planted["crop_a"], "씨앗 먼저 → 입고 12개 (120달러)");
            Assert.AreEqual(16, d1.OpenCells, "남은 180달러로는 250 구획을 못 삼");
            Assert.AreEqual(180, d1.Money);
        }

        [Test]
        public void PlaySessionLog_AddsTimeAndWritesTable()
        {
            var log = new PlaySessionLog();
            log.AddTime(1, DayPhase.Farming, 95.4);
            log.AddTime(1, DayPhase.Farming, 30);
            log.AddTime(1, DayPhase.Sales, 31);
            log.AddTime(1, DayPhase.Shop, -5);
            log.DayOf(1).Settlement = new SettlementReport(1, true, 8, 7, 1, 6, 90, null, 20, new ExpGain(90, 1, 3), 2);
            log.DayOf(1).Cleanliness = 0.75;
            Assert.AreEqual(156.4, log.DayOf(1).TotalSeconds, 1e-6);
            string md = log.Markdown("기록");
            StringAssert.Contains("| 1 | 0:00 | 2:05 | 0:00 | 0:31 | 0:00 | 2:36 | 75 % | 8 | 6 | 90 | 20 | 70 |", md);
            StringAssert.Contains("합계 플레이 시간: 2:36", md);
            Assert.AreEqual("10:05", PlaySessionLog.Time(605));
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

        // ── P-6 재측정: 봇 규칙 7 (괭이) · 클릭 수 · 새 목표 판정 ──

        private static ProductSpec Hoe() => new ProductSpec("shop_hoe", new[] { new TierSpec(300), new TierSpec(1200) }, false);

        [Test]
        public void Rule7_HoeBought_OnlyWithMoneyLeftAfterSeeds()
        {
            var config = Config(money: 400);
            config.PlotProduct = default; // 구획 없이 괭이 규칙만
            config.HoeProduct = Hoe();
            var d1 = DaySimulator.Run(config, 1).Days[0];
            Assert.AreEqual(12, d1.Planted["crop_a"], "씨앗 먼저 (입고 12 = 120달러)");
            Assert.AreEqual(280, d1.Money, "400 − 씨앗 120");
            Assert.AreEqual(0, d1.HoeBought, "280 < 300 → 괭이 안 삼");
            Assert.AreEqual(0, d1.HoeTier);

            config.StartMoney = 420;
            var bought = DaySimulator.Run(config, 1).Days[0];
            Assert.AreEqual(1, bought.HoeBought, "420 − 120 = 300 ≥ 300 → 다음 등급");
            Assert.AreEqual(1, bought.HoeTier);
            Assert.AreEqual(0, bought.Money);
            Assert.AreEqual(420, bought.Spent);
        }

        [Test]
        public void TillClicks_FollowHoeWork()
        {
            var config = Config(money: 40); // 씨앗 4개
            config.PlotProduct = default;
            config.TillWorkRequired = 3;
            config.HoeWorkPerTier = new double[] { 1 };
            var d1 = DaySimulator.Run(config, 1).Days[0];
            Assert.AreEqual(4, d1.PlantClicks);
            Assert.AreEqual(12, d1.TillClicks, "낡은 괭이(1) × 작업량 3 = 칸당 3번");
            Assert.AreEqual(4, d1.WaterClicks, "물뿌리개 1칸");
            Assert.AreEqual(0, d1.HarvestClicks);
            Assert.AreEqual(20, d1.FarmClicks, "개간 12 + 심기 4 + 물 4");

            config.HoeWorkPerTier = new double[] { 2 };
            config.WateringCells = 4;
            var fast = DaySimulator.Run(config, 1).Days[0];
            Assert.AreEqual(8, fast.TillClicks, "작업량 2 → 칸당 2번 (3 ÷ 2 올림)");
            Assert.AreEqual(1, fast.WaterClicks, "4칸 물뿌리개 → 1번");

            var d2 = DaySimulator.Run(config, 1).Days[1];
            Assert.AreEqual(4, d2.HarvestClicks, "다음 날 4칸 수확");
        }

        [Test]
        public void CleaningClicks_FromMessRules()
        {
            var config = Config(money: 100);
            config.PlotProduct = default;
            // 1일차 쓰레기 30 · 얼룩 12, 평소 쓰레기 1~1, 얼룩 = 어제 손님 × 0.5, 흩어짐 = 안 팔린 진열 × 0.5 + 손님 ÷ 4, 흐트러짐 = 찬 칸 × 0.5
            config.Mess = new Logic.Cleaning.MessSpec(30, 12, 1, 1, 0.5, 0.5, 4, 0.5, 0.95, 0.8, 0.6);
            config.Cleanliness = 1;
            var run = DaySimulator.Run(config, 1);
            var d1 = run.Days[0];
            Assert.AreEqual(30, d1.Trash);
            Assert.AreEqual(12, d1.Stains);
            Assert.AreEqual(42, d1.CleanClicks, "모두 치움 (청결도 1)");
            Assert.AreEqual(0, d1.TidyClicks, "1일차는 흩어짐 · 흐트러짐 없음");

            var d2 = run.Days[1];
            Assert.AreEqual(1, d2.Trash);
            Assert.AreEqual(4, d2.Stains, "어제 손님 8 × 0.5");
            Assert.AreEqual(2, d2.Scattered, "안 팔린 진열 0 × 0.5 + 8 ÷ 4");
            Assert.AreEqual(d2.Scattered * 2 + d2.Disordered, d2.TidyClicks, "줍기 + 다시 진열 2번 · 흐트러진 칸 1번");
            Assert.AreEqual(10, d2.DisplayClicks, "수확 10개 진열");
        }

        [Test]
        public void NewGoals_Customers_Scattered_UnlockAtDayEnd()
        {
            var config = Config(money: 0);
            config.StartCrops["crop_a"] = 6; // 1일차 매출 90 → Lv2 · SP 3
            config.SkillPriority.Add(new SimSkill { Id = "skill_b", Costs = new[] { 3 } });
            config.Mess = new Logic.Cleaning.MessSpec(0, 0, 0, 0, 0, 0, 3, 0, 0.95, 0.8, 0.6);
            config.Goals.Add(new SimGoal { Day = 1, Kind = SimGoalKind.CustomersAtLeast, Amount = 1, Label = "첫 손님" });
            config.Goals.Add(new SimGoal { Day = 1, Kind = SimGoalKind.CropUnlocked, CropId = "crop_b", Label = "아침 해금" });
            config.Goals.Add(new SimGoal { Day = 1, Kind = SimGoalKind.CropUnlockedAtDayEnd, CropId = "crop_b", Label = "정산 해금" });
            config.Goals.Add(new SimGoal { Day = 2, Kind = SimGoalKind.ScatteredAtLeast, Amount = 1, Label = "흩어짐" });
            var run = DaySimulator.Run(config, 1);
            CollectionAssert.AreEqual(new[] { true, false, true }, run.Days[0].Goals.Select(g => g.Value).ToArray(), "1일차 정산 뒤 스킬로 해금 = 그날 끝 해금");
            Assert.IsTrue(run.Days[1].Goals[0].Value, "어제 손님 8 ÷ 3 → 흩어진 상품 3");
        }

        [Test]
        public void StockFromNextDay_CountsFromSkillDay_LikeGame()
        {
            // 1일차 정산 뒤 스킬로 해금 → 게임은 그 순간 열린 날 = 1일차 → 2일차부터 입고 (ShopSystem.OnSkillChanged)
            var config = Config(money: 0);
            config.StartCrops["crop_a"] = 6;
            config.Crops[1].SeedProduct = new ProductSpec("shop_b",
                new[] { new TierSpec(20, new[] { new UnlockRequirement(UnlockKind.SkillLevel, "skill_b", 1) }) }, true, new[] { 8 }, stockFromNextDay: true);
            config.SkillPriority.Add(new SimSkill { Id = "skill_b", Costs = new[] { 3 } });
            var d2 = DaySimulator.Run(config, 1).Days[1];
            Assert.IsTrue(d2.Planted.ContainsKey("crop_b") && d2.Planted["crop_b"] > 0, "배운 다음 날 바로 입고 → 심음");
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
