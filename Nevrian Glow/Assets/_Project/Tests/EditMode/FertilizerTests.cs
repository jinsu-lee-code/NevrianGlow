using NevrianGlowFarm.Logic.Farm;
using NevrianGlowFarm.Logic.Save;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>비료 (기획 shop.csv 10/30): 주는 즉시 1일 성장, 작물당 1회.</summary>
    public class FertilizerTests
    {
        // 기획 crops.csv 10/30: 월광 1일 · 버섯꽃 2일(물 없음) · 등불 4일, 재수확 2일 × 5회
        private static readonly CropSpec Moonroot = new CropSpec(1, 1, 1, 0, 1);
        private static readonly CropSpec Mushroom = new CropSpec(2, 2, 3, 0, 1);
        private static readonly CropSpec Lantern = new CropSpec(4, 1, 5, 2, 5);

        private static FarmCell Planted(string id)
        {
            var c = new FarmCell();
            FarmRules.TryTill(c, 3f, 3f);
            FarmRules.TryPlant(c, id);
            return c;
        }

        private static int NightsUntilReady(FarmCell c, CropSpec spec, int max = 20)
        {
            for (int night = 1; night <= max; night++)
            {
                FarmRules.TryWater(c, spec);
                FarmRules.GrowOneNight(c, spec);
                if (FarmRules.IsReady(c, spec))
                    return night;
            }
            return -1;
        }

        [Test]
        public void Moonroot_ReadyTheDayItIsPlanted()
        {
            // "월광은 심은 날 바로 수확"
            var c = Planted("crop_moonroot");
            Assert.IsTrue(FarmRules.TryFertilize(c, Moonroot, 1));
            Assert.IsTrue(FarmRules.IsReady(c, Moonroot));
        }

        [Test]
        public void Mushroom_OneNightInsteadOfTwo()
        {
            var with = Planted("crop_mushroomflower");
            Assert.IsTrue(FarmRules.TryFertilize(with, Mushroom, 1));
            Assert.AreEqual(1, with.Age, "준 순간 하루 자람");
            Assert.AreEqual(1, NightsUntilReady(with, Mushroom));

            var without = Planted("crop_mushroomflower");
            Assert.AreEqual(2, NightsUntilReady(without, Mushroom));
        }

        [Test]
        public void GrowsEvenWhenDry_NotPastReady()
        {
            var c = Planted("crop_lanternfruit");
            Assert.IsTrue(FarmRules.TryFertilize(c, Lantern, 9));
            Assert.AreEqual(4, c.Age, "다 자란 날을 넘지 않음");
            Assert.IsTrue(FarmRules.IsReady(c, Lantern));
        }

        [Test]
        public void OncePerCrop()
        {
            var c = Planted("crop_lanternfruit");
            Assert.IsTrue(FarmRules.TryFertilize(c, Lantern, 1));
            Assert.IsFalse(FarmRules.TryFertilize(c, Lantern, 1), "같은 날 두 번");
            FarmRules.TryWater(c, Lantern);
            FarmRules.GrowOneNight(c, Lantern);
            Assert.IsFalse(FarmRules.TryFertilize(c, Lantern, 1), "다음 날에도");
        }

        [Test]
        public void ReadyOrEmpty_CannotFertilize()
        {
            var empty = new FarmCell();
            Assert.IsFalse(FarmRules.TryFertilize(empty, Moonroot, 1));

            var ready = Planted("crop_moonroot");
            NightsUntilReady(ready, Moonroot);
            Assert.IsFalse(FarmRules.TryFertilize(ready, Moonroot, 1));
            Assert.IsFalse(FarmRules.TryFertilize(Planted("crop_moonroot"), Moonroot, 0), "0일 비료");
        }

        [Test]
        public void FirstGrowthOnly_RegrowKeepsItsDays_EndsWithCrop()
        {
            var lantern = Planted("crop_lanternfruit");
            FarmRules.TryFertilize(lantern, Lantern, 1);
            Assert.AreEqual(3, NightsUntilReady(lantern, Lantern), "첫 성장 4 → 3");
            FarmRules.TryHarvest(lantern, Lantern);
            Assert.AreEqual(2, NightsUntilReady(lantern, Lantern), "재수확 간격 2는 그대로");
            Assert.IsFalse(FarmRules.TryFertilize(lantern, Lantern, 1), "같은 포기에는 다시 못 줌");

            var moon = Planted("crop_moonroot");
            FarmRules.TryFertilize(moon, Moonroot, 1);
            FarmRules.TryHarvest(moon, Moonroot);
            Assert.IsFalse(moon.IsFertilized, "작물이 사라지면 표시도 사라짐");
        }

        [Test]
        public void Save_KeepsFertilized_AndOldPendingBecomesMarkOnly()
        {
            var farm = new FarmState(4, 1, id => Mushroom);
            FarmRules.TryTill(farm.Cells[0], 3f, 3f);
            FarmRules.TryPlant(farm.Cells[0], "crop_mushroomflower");
            FarmRules.TryFertilize(farm.Cells[0], Mushroom, 1);
            var s = new SaveSnapshot();
            SaveState.CaptureFarm(farm, s);
            Assert.IsTrue(SaveFormat.TryRead(SaveFormat.Write(s), out var back, out string error), error);
            Assert.AreEqual(1, back.Cells[0].FertilizerBonus);
            Assert.AreEqual(1, back.Cells[0].Age);

            // 10/30 전 파일: 오늘 준 비료(마지막 칸 1)는 밤에 준 표시만 — 성장일은 그대로 2
            Assert.IsTrue(SaveFormat.TryRead("ngf_save 1\ncell 0 1 3 crop_mushroomflower 0 1 0 0 1\nend\n", out var old, out error), error);
            var farm2 = new FarmState(4, 1, id => Mushroom);
            SaveState.ApplyFarm(farm2, old);
            var cell = farm2.Cells[0];
            FarmRules.GrowOneNight(cell, Mushroom);
            Assert.IsTrue(cell.IsFertilized);
            Assert.AreEqual(2, FarmRules.RequiredDays(cell, Mushroom));
        }
    }
}
