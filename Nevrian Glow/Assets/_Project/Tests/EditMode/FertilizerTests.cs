using NevrianGlowFarm.Logic.Farm;
using NevrianGlowFarm.Logic.Save;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class FertilizerTests
    {
        // BALANCE 2-2 v1
        private static readonly CropSpec Moonroot = new CropSpec(1, 1, 1, 0, 1);
        private static readonly CropSpec Mushroom = new CropSpec(2, 2, 2, 0, 1);
        private static readonly CropSpec Lantern = new CropSpec(8, 1, 2, 4, 3);

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
        public void Mushroom_PlantedWithFertilizer_ReadyNextDay_Balance31()
        {
            // BALANCE 3-1 비료 행: 버섯꽃 1일차 심기 + 비료 → 2일차 (비료 없으면 3일차)
            var with = Planted("crop_mushroomflower");
            Assert.IsTrue(FarmRules.TryFertilize(with, Mushroom, 1));
            Assert.AreEqual(1, NightsUntilReady(with, Mushroom));

            var without = Planted("crop_mushroomflower");
            Assert.AreEqual(2, NightsUntilReady(without, Mushroom));
        }

        [Test]
        public void OncePerCrop()
        {
            var c = Planted("crop_lanternfruit");
            Assert.IsTrue(FarmRules.TryFertilize(c, Lantern, 1));
            Assert.IsFalse(FarmRules.TryFertilize(c, Lantern, 1), "같은 날 두 번");
            FarmRules.GrowOneNight(c, Lantern);
            Assert.IsFalse(FarmRules.TryFertilize(c, Lantern, 1), "효과가 들어간 뒤에도");
        }

        [Test]
        public void MinimumOneDay_MoonrootStillNeedsOneNight()
        {
            var c = Planted("crop_moonroot");
            Assert.IsTrue(FarmRules.TryFertilize(c, Moonroot, 1));
            Assert.IsFalse(FarmRules.IsReady(c, Moonroot), "심은 날 바로 다 자라지 않음");
            Assert.AreEqual(1, NightsUntilReady(c, Moonroot));
            Assert.AreEqual(1, FarmRules.RequiredDays(c, Moonroot));
        }

        [Test]
        public void AlreadyGrownEnough_ReadyNextMorning_NotNow()
        {
            // GDD 4-9: 이미 자란 일수가 줄어든 필요 성장일 이상이면 다음 아침 수확 가능
            var c = Planted("crop_mushroomflower");
            FarmRules.TryWater(c, Mushroom);
            FarmRules.GrowOneNight(c, Mushroom); // 1일 자람 (필요 2)
            Assert.IsTrue(FarmRules.TryFertilize(c, Mushroom, 1));
            Assert.AreEqual(FarmCellStatus.Growing, FarmRules.GetStatus(c, Mushroom), "준 순간에는 그대로");

            FarmRules.GrowOneNight(c, Mushroom); // 물이 없어도 비료 효과는 들어간다
            Assert.AreEqual(FarmCellStatus.Ready, FarmRules.GetStatus(c, Mushroom));
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
        public void EffectEndsWithCrop_KeptForRegrow()
        {
            var lantern = Planted("crop_lanternfruit");
            FarmRules.TryFertilize(lantern, Lantern, 1);
            Assert.AreEqual(7, NightsUntilReady(lantern, Lantern), "첫 성장 8 → 7");
            FarmRules.TryHarvest(lantern, Lantern);
            Assert.AreEqual(3, NightsUntilReady(lantern, Lantern), "같은 포기의 재수확 4 → 3");

            var moon = Planted("crop_moonroot");
            FarmRules.TryFertilize(moon, Moonroot, 1);
            NightsUntilReady(moon, Moonroot);
            FarmRules.TryHarvest(moon, Moonroot);
            Assert.IsFalse(moon.IsFertilized, "작물이 사라지면 효과도 사라짐");
        }

        [Test]
        public void Save_KeepsPendingFertilizer_AndReadsOldCellLine()
        {
            var farm = new FarmState(4, 1, id => Mushroom);
            FarmRules.TryTill(farm.Cells[0], 3f, 3f);
            FarmRules.TryPlant(farm.Cells[0], "crop_mushroomflower");
            FarmRules.TryFertilize(farm.Cells[0], Mushroom, 1);
            var s = new SaveSnapshot();
            SaveState.CaptureFarm(farm, s);
            Assert.IsTrue(SaveFormat.TryRead(SaveFormat.Write(s), out var back, out string error), error);
            Assert.AreEqual(1, back.Cells[0].PendingFertilizer);

            // 4-6 이전 파일: cell 줄에 오늘 준 비료 칸이 없다 → 0
            Assert.IsTrue(SaveFormat.TryRead("ngf_save 1\ncell 0 1 3 crop_moonroot 0 1 0 0\nend\n", out var old, out error), error);
            Assert.AreEqual(0, old.Cells[0].PendingFertilizer);
        }
    }
}
