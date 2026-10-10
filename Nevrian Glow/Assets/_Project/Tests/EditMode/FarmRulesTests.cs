using System;
using System.Collections.Generic;
using NevrianGlowFarm.Logic.Farm;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>BALANCE 3-1 (성장 타이밍) · 3-1b (개간 · 칸 상태).</summary>
    public class FarmRulesTests
    {
        // BALANCE 2-2 (v1)
        private static readonly CropSpec Moonroot = new CropSpec(1, 1, 1, 0, 1);
        private static readonly CropSpec Mushroom = new CropSpec(2, 2, 2, 0, 1);
        private static readonly CropSpec Lantern = new CropSpec(8, 1, 2, 4, 3);
        private const float TillWork = 3f;

        private static FarmCell TilledCell()
        {
            var cell = new FarmCell();
            while (!cell.IsTilled)
                FarmRules.TryTill(cell, 1f, TillWork);
            return cell;
        }

        /// <summary>plantDay에 심고, waterOn(일차)이면 그날 물을 준다. 수확 가능한 날들을 돌려준다 (수확하면 다음 날로).</summary>
        private static List<int> HarvestDays(CropSpec spec, Func<int, bool> waterOn, int plantDay = 1, int lastDay = 30)
        {
            var cell = TilledCell();
            FarmRules.TryPlant(cell, "crop");
            var days = new List<int>();
            for (int day = plantDay; day <= lastDay && cell.HasCrop; day++)
            {
                if (FarmRules.IsReady(cell, spec))
                {
                    days.Add(day);
                    FarmRules.TryHarvest(cell, spec);
                    if (!cell.HasCrop)
                        break;
                }
                if (waterOn(day))
                    FarmRules.TryWater(cell, spec);
                FarmRules.GrowOneNight(cell, spec);
                FarmRules.ResetIfEmpty(cell);
            }
            return days;
        }

        // ── 3-1 성장 타이밍 ──

        [Test]
        public void Moonroot_WaterDayOne_HarvestDayTwo()
        {
            CollectionAssert.AreEqual(new[] { 2 }, HarvestDays(Moonroot, d => d == 1));
        }

        [Test]
        public void Mushroom_WaterDayOneOnly_WetTwoDays_HarvestDayThree()
        {
            CollectionAssert.AreEqual(new[] { 3 }, HarvestDays(Mushroom, d => d == 1));
        }

        [Test]
        public void Mushroom_UnlockedDayThree_PlantAndWater_HarvestDayFive()
        {
            CollectionAssert.AreEqual(new[] { 5 }, HarvestDays(Mushroom, d => d == 3, plantDay: 3));
        }

        [Test]
        public void Lantern_WaterDaily_HarvestDays9_13_17_ThenRemoved()
        {
            CollectionAssert.AreEqual(new[] { 9, 13, 17 }, HarvestDays(Lantern, d => true));
        }

        [Test]
        public void NoWater_StopsGrowing_NoWithering_ResumesWhenWatered()
        {
            var cell = TilledCell();
            FarmRules.TryPlant(cell, "crop");

            for (int night = 0; night < 3; night++)
                FarmRules.GrowOneNight(cell, Moonroot);
            Assert.AreEqual(0, cell.Age, "물 없이는 자라지 않음");
            Assert.IsTrue(cell.HasCrop, "시들지 않음");

            FarmRules.TryWater(cell, Moonroot);
            FarmRules.GrowOneNight(cell, Moonroot);
            Assert.IsTrue(FarmRules.IsReady(cell, Moonroot), "물 준 다음 날 수확");
        }

        [Test]
        public void Mushroom_WaterOnce_StopsAfterWetRunsOut()
        {
            var spec = new CropSpec(4, 2, 2, 0, 1); // 이전 값(성장 4일)으로 수분 2일 유지 확인
            var cell = TilledCell();
            FarmRules.TryPlant(cell, "crop");
            FarmRules.TryWater(cell, spec);
            for (int night = 0; night < 5; night++)
                FarmRules.GrowOneNight(cell, spec);

            Assert.AreEqual(2, cell.Age);
            Assert.IsFalse(FarmRules.IsReady(cell, spec));
        }

        [Test]
        public void Water_RefillsUpToWetDays_FullOrReadyIsNotAChange()
        {
            var cell = TilledCell();
            FarmRules.TryPlant(cell, "crop");

            Assert.IsTrue(FarmRules.TryWater(cell, Mushroom));
            Assert.AreEqual(2, cell.WetDaysLeft);
            Assert.IsFalse(FarmRules.TryWater(cell, Mushroom), "이미 가득");

            FarmRules.GrowOneNight(cell, Mushroom);
            Assert.IsTrue(FarmRules.TryWater(cell, Mushroom), "하루 줄면 다시 채움");

            FarmRules.GrowOneNight(cell, Mushroom);
            Assert.IsTrue(FarmRules.IsReady(cell, Mushroom));
            Assert.IsFalse(FarmRules.TryWater(cell, Mushroom), "다 자란 작물에는 물을 주지 않음");
        }

        // ── 3-1b 개간 · 칸 상태 ──

        [TestCase(1f, 3)]
        [TestCase(1.5f, 2)]
        [TestCase(3f, 1)]
        public void Hoe_TillsInExpectedHits(float workPerUse, int expectedHits)
        {
            var cell = new FarmCell();
            int hits = 0;
            while (!cell.IsTilled && hits < 10)
            {
                FarmRules.TryTill(cell, workPerUse, TillWork);
                hits++;
            }

            Assert.AreEqual(expectedHits, hits);
            Assert.AreEqual(FarmCellStatus.Tilled, FarmRules.GetStatus(cell, default));
            Assert.IsFalse(FarmRules.TryTill(cell, workPerUse, TillWork), "개간된 칸은 더 갈지 않음");
        }

        [Test]
        public void PartlyTilled_Night_BackToUntilled()
        {
            var cell = new FarmCell();
            FarmRules.TryTill(cell, 1f, TillWork);
            FarmRules.TryTill(cell, 1f, TillWork);
            Assert.AreEqual(FarmCellStatus.Tilling, FarmRules.GetStatus(cell, default));

            Assert.IsTrue(FarmRules.ResetIfEmpty(cell));
            Assert.AreEqual(FarmCellStatus.Untilled, FarmRules.GetStatus(cell, default));
            Assert.AreEqual(0f, cell.TillProgress);
        }

        [Test]
        public void TilledButEmpty_Night_BackToUntilled()
        {
            var cell = TilledCell();

            Assert.IsTrue(FarmRules.ResetIfEmpty(cell));
            Assert.AreEqual(FarmCellStatus.Untilled, FarmRules.GetStatus(cell, default));
        }

        [Test]
        public void PlantedCell_Night_KeepsCrop()
        {
            var cell = TilledCell();
            FarmRules.TryPlant(cell, "crop");

            Assert.IsFalse(FarmRules.ResetIfEmpty(cell));
            Assert.IsTrue(cell.HasCrop);
        }

        [Test]
        public void UntilledCell_CannotPlantOrWater()
        {
            var cell = new FarmCell();

            Assert.IsFalse(FarmRules.TryPlant(cell, "crop"));
            Assert.IsFalse(FarmRules.TryWater(cell, Moonroot));
            Assert.AreEqual(FarmCellStatus.Untilled, FarmRules.GetStatus(cell, default));
        }

        [Test]
        public void PlantedCell_CannotTillOrPlantAgain()
        {
            var cell = TilledCell();
            FarmRules.TryPlant(cell, "crop");

            Assert.IsFalse(FarmRules.TryTill(cell, 1f, TillWork));
            Assert.IsFalse(FarmRules.TryPlant(cell, "other"));
        }

        [Test]
        public void MoonrootHarvest_CellBackToUntilled()
        {
            var cell = TilledCell();
            FarmRules.TryPlant(cell, "crop_moonroot");
            FarmRules.TryWater(cell, Moonroot);
            FarmRules.GrowOneNight(cell, Moonroot);

            var result = FarmRules.TryHarvest(cell, Moonroot);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("crop_moonroot", result.CropId);
            Assert.AreEqual(1, result.Yield);
            Assert.IsTrue(result.CropRemoved);
            Assert.AreEqual(FarmCellStatus.Untilled, FarmRules.GetStatus(cell, Moonroot));
        }

        [Test]
        public void Lantern_FirstTwoHarvestsKeepCell_ThirdRemoves()
        {
            var cell = TilledCell();
            FarmRules.TryPlant(cell, "crop_lanternfruit");

            for (int harvest = 1; harvest <= 3; harvest++)
            {
                while (!FarmRules.IsReady(cell, Lantern))
                {
                    FarmRules.TryWater(cell, Lantern);
                    FarmRules.GrowOneNight(cell, Lantern);
                }
                var result = FarmRules.TryHarvest(cell, Lantern);
                Assert.AreEqual(2, result.Yield);
                Assert.AreEqual(harvest == 3, result.CropRemoved, $"{harvest}회차");
                Assert.AreEqual(harvest < 3, cell.HasCrop, $"{harvest}회차 뒤 칸 유지");
            }
            Assert.AreEqual(FarmCellStatus.Untilled, FarmRules.GetStatus(cell, Lantern));
        }

        [Test]
        public void NotReady_HarvestFails_NothingChanges()
        {
            var cell = TilledCell();
            FarmRules.TryPlant(cell, "crop");

            Assert.IsFalse(FarmRules.TryHarvest(cell, Moonroot).Succeeded);
            Assert.IsTrue(cell.HasCrop);
        }
    }
}
