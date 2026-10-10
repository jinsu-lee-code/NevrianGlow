using NevrianGlowFarm.Logic.Farm;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>BALANCE 2-3 · 9-1: cellPitch 2 / bedPitch 5.6 / plotPitch 11.2, 구획 16칸.</summary>
    public class FarmLayoutTests
    {
        private const double Cell = 2, Bed = 5.6, Plot = 11.2, Eps = 1e-6;

        private static (double Along, double Across) At(int index) => FarmLayout.CellOffset(index, 16, Cell, Bed, Plot);

        [Test]
        public void FirstBed_IsCenteredOnOrigin()
        {
            Assert.AreEqual(-1, At(0).Along, Eps);
            Assert.AreEqual(-1, At(0).Across, Eps);
            Assert.AreEqual(1, At(3).Along, Eps);
            Assert.AreEqual(1, At(3).Across, Eps);
        }

        [Test]
        public void PairInBed_IsXorOne_OneCellApart()
        {
            for (int i = 0; i < 16; i++)
            {
                var a = At(i);
                var b = At(i ^ 1);
                Assert.AreEqual(Cell, System.Math.Abs(a.Along - b.Along), Eps, $"칸 {i}");
                Assert.AreEqual(a.Across, b.Across, Eps);
            }
        }

        [Test]
        public void NeighborBedInRow_IsFourCellsLater_OneBedApart()
        {
            Assert.AreEqual(Bed, At(4).Along - At(0).Along, Eps);
            Assert.AreEqual(At(0).Across, At(4).Across, Eps, "같은 줄 (i & ~7)");
        }

        [Test]
        public void SecondRow_IsAcrossThePath()
        {
            Assert.AreEqual(Bed, At(8).Across - At(0).Across, Eps);
            Assert.AreEqual(At(0).Along, At(8).Along, Eps);
        }

        [Test]
        public void NextPlot_IsOnePlotPitchAlong()
        {
            Assert.AreEqual(Plot, At(16).Along - At(0).Along, Eps);
            Assert.AreEqual(At(0).Across, At(16).Across, Eps);
        }

        [Test]
        public void Rows_LeavePathBetween_BALANCE91()
        {
            // 원점 z = -2.8 기준: 첫 줄 칸 중심 z = -3.8 · -1.8, 둘째 줄 = 1.8 · 3.8 → 길(z ±1) 바깥
            const double originZ = -2.8;
            Assert.AreEqual(-1.8, originZ + At(2).Across, Eps);
            Assert.AreEqual(1.8, originZ + At(8).Across, Eps);
        }
    }
}
