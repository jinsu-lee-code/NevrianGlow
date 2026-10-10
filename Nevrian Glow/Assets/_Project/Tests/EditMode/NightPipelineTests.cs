using System.Linq;
using NevrianGlowFarm.Logic.Farm;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class NightPipelineTests
    {
        private static readonly CropSpec Moonroot = new CropSpec(1, 1, 1, 0, 1);

        [Test]
        public void StepOrder_MatchesGdd()
        {
            var names = new NightPipeline().Steps.Select(s => s.Name).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                NightPipeline.Sprinkler, NightPipeline.FertilizerSprayer, NightPipeline.Growth, NightPipeline.GiantCrop,
                NightPipeline.AutoHarvester, NightPipeline.ResetEmptyCells, NightPipeline.Processing, NightPipeline.ShopRestock,
            }, names);
        }

        [Test]
        public void Night_GrowsWateredCrops_AndResetsEmptyTilledCells()
        {
            var farm = new FarmState(16, 2, _ => Moonroot);
            var planted = farm.Cells[0];
            var tilledOnly = farm.Cells[1];
            for (int i = 0; i < 3; i++)
            {
                FarmRules.TryTill(planted, 1f, 3f);
                FarmRules.TryTill(tilledOnly, 1f, 3f);
            }
            FarmRules.TryPlant(planted, "crop_moonroot");
            FarmRules.TryWater(planted, Moonroot);

            new NightPipeline().Run(farm);

            Assert.AreEqual(FarmCellStatus.Ready, farm.StatusOf(0));
            Assert.AreEqual(FarmCellStatus.Untilled, farm.StatusOf(1));
            Assert.AreEqual(32, farm.Cells.Count);
        }

        [Test]
        public void Replace_SwapsStepInPlace()
        {
            var pipeline = new NightPipeline();
            var giant = new CountingStep(NightPipeline.GiantCrop);

            pipeline.Replace(NightPipeline.GiantCrop, giant);
            pipeline.Run(new FarmState(16, 1, _ => Moonroot));

            Assert.AreEqual(1, giant.Runs);
            Assert.AreEqual(3, pipeline.Steps.ToList().IndexOf(giant), "자리는 그대로 (성장 다음)");
            Assert.Throws<System.ArgumentException>(() => pipeline.Replace("없는 단계", giant));
        }

        [Test]
        public void PlotCount_GrowsOnly()
        {
            var farm = new FarmState(16, 2, _ => Moonroot);

            farm.SetPlotCount(3);

            Assert.AreEqual(48, farm.Cells.Count);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => farm.SetPlotCount(1));
        }

        private sealed class CountingStep : INightStep
        {
            public CountingStep(string name) => Name = name;
            public string Name { get; }
            public int Runs { get; private set; }
            public void Run(FarmState farm) => Runs++;
        }
    }
}
