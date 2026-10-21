using NevrianGlowFarm.Logic.Farm;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>P-5 밭 배치 (레벨배치 설정안 1절): cellPitch 2 / bedPitch 4 / plotPitch 8, 구획 16칸 — 밭 4 × 4 m가 빈틈 없이 붙는다.</summary>
    public class FarmLayoutTests
    {
        private const double Cell = 2, Bed = 4, Plot = 8, Eps = 1e-6;

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
        public void SecondRow_IsOneBedAcross()
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
        public void StartTwoPlots_AreFourByTwoBeds_WithoutGaps()
        {
            // 시작 2구획 = 32칸 → 칸 중심이 along 8줄 · across 4줄, 모두 칸 간격(2 m)으로 이어짐 = 16 × 8 m (밭 4 × 2)
            var b = FarmLayout.AreaCenterBounds(2, 16, Cell, Bed, Plot);
            Assert.AreEqual(16, b.AlongMax - b.AlongMin + Cell, Eps, "밭 4개 × 4 m");
            Assert.AreEqual(8, b.AcrossMax - b.AcrossMin + Cell, Eps, "밭 2개 × 4 m");

            var alongs = new System.Collections.Generic.SortedSet<double>();
            var acrosses = new System.Collections.Generic.SortedSet<double>();
            for (int i = 0; i < 32; i++)
            {
                alongs.Add(System.Math.Round(At(i).Along, 6));
                acrosses.Add(System.Math.Round(At(i).Across, 6));
            }
            Assert.AreEqual(8, alongs.Count);
            Assert.AreEqual(4, acrosses.Count);
            double previous = double.NaN;
            foreach (double a in alongs)
            {
                if (!double.IsNaN(previous))
                    Assert.AreEqual(Cell, a - previous, Eps, "밭 사이 빈틈 없음");
                previous = a;
            }
        }
    }
}
