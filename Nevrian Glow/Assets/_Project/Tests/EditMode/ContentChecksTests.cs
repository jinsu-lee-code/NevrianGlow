using System.Linq;
using NevrianGlowFarm.Logic.Data;
using NevrianGlowFarm.Logic.Farm;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class ContentChecksTests
    {
        [Test]
        public void ValidIds_NoProblems()
        {
            var problems = ContentChecks.CheckIds(new[] { ("crop_moonroot", "a"), ("tool_hoe_1", "b") });

            Assert.IsEmpty(problems);
        }

        [Test]
        public void Empty_Duplicate_InvalidChars_AreReported()
        {
            var problems = ContentChecks.CheckIds(new[]
            {
                ("", "a.asset"),
                ("crop_moonroot", "b.asset"),
                ("crop_moonroot", "c.asset"),
                ("Crop-Bad", "d.asset"),
            });

            Assert.AreEqual(3, problems.Count);
            Assert.IsTrue(problems.Any(p => p.Source == "a.asset"), "빈 id");
            Assert.IsTrue(problems.Any(p => p.Source == "c.asset" && p.Message.Contains("b.asset")), "중복은 처음 것을 알려 줌");
            Assert.IsTrue(problems.Any(p => p.Source == "d.asset"), "대문자 · 하이픈");
        }

        // BALANCE 3-5 칸당 일일 이익 (v1 수치)
        [Test]
        public void ProfitPerCellPerDay_MatchesBalance35()
        {
            Assert.AreEqual(5.0, CropEconomics.ProfitPerCellPerDay(new CropSpec(1, 1, 1, 0, 1), 10, 15), 1e-9, "월광");
            Assert.AreEqual(30.0, CropEconomics.ProfitPerCellPerDay(new CropSpec(2, 2, 2, 0, 1), 20, 40), 1e-9, "버섯꽃 (성장 2일)");
            Assert.AreEqual(25.625, CropEconomics.ProfitPerCellPerDay(new CropSpec(8, 1, 2, 4, 3), 70, 80), 1e-9, "등불 3회");
        }

        [Test]
        public void Lantern_TotalDays16_ThreeHarvests()
        {
            var lantern = new CropSpec(8, 1, 2, 4, 3);

            Assert.AreEqual(16, CropEconomics.TotalDays(lantern));
            Assert.AreEqual(3, CropEconomics.HarvestCount(lantern));
            Assert.AreEqual(410, CropEconomics.ProfitPerPlant(lantern, 70, 80));
        }

        [Test]
        public void MaxHarvestsWithoutRegrow_CountsOnce()
        {
            var spec = new CropSpec(3, 1, 1, 0, 3);

            Assert.AreEqual(1, CropEconomics.HarvestCount(spec));
            Assert.AreEqual(3, CropEconomics.TotalDays(spec));
        }
    }
}
