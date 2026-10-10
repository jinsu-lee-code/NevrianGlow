using NevrianGlowFarm.Logic.Farm;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>BALANCE 2-11 물뿌리개 1 · 2 · 4 · 8칸 + 칸 번호 규칙 (FarmLayout).</summary>
    public class WateringPatternTests
    {
        private const int Total = 32; // 시작 2구획

        [Test]
        public void One_IsJustThatCell()
        {
            CollectionAssert.AreEqual(new[] { 6 }, WateringPattern.Cells(6, 1, Total));
        }

        [Test]
        public void Two_IsPairInBed_XorOne()
        {
            CollectionAssert.AreEquivalent(new[] { 4, 5 }, WateringPattern.Cells(5, 2, Total));
            CollectionAssert.AreEquivalent(new[] { 6, 7 }, WateringPattern.Cells(6, 2, Total));
        }

        [Test]
        public void Two_Across_IsPairInBed_XorTwo()
        {
            CollectionAssert.AreEqual(new[] { 5, 7 }, WateringPattern.Cells(5, 2, Total, PairDirection.Across));
            CollectionAssert.AreEqual(new[] { 4, 6 }, WateringPattern.Cells(6, 2, Total, PairDirection.Across));
            CollectionAssert.AreEqual(new[] { 4, 5 }, WateringPattern.Cells(5, 2, Total, PairDirection.Along));
        }

        [Test]
        public void Two_MatchesLayout_AlongAndAcrossNeighbors()
        {
            // i^1 = along만 다름(across 같음), i^2 = across만 다름(along 같음) — 둘 다 붙어 있는 옆 칸
            var c = FarmLayout.CellOffset(5, 16, 2, 5.6, 11.2);
            var along = FarmLayout.CellOffset(5 ^ 1, 16, 2, 5.6, 11.2);
            var across = FarmLayout.CellOffset(5 ^ 2, 16, 2, 5.6, 11.2);
            Assert.AreEqual(c.Across, along.Across, 1e-9);
            Assert.AreEqual(2.0, System.Math.Abs(c.Along - along.Along), 1e-9);
            Assert.AreEqual(c.Along, across.Along, 1e-9);
            Assert.AreEqual(2.0, System.Math.Abs(c.Across - across.Across), 1e-9);
        }

        [Test]
        public void Direction_OnlyAffectsTwo()
        {
            CollectionAssert.AreEqual(WateringPattern.Cells(6, 4, Total), WateringPattern.Cells(6, 4, Total, PairDirection.Across));
            CollectionAssert.AreEqual(WateringPattern.Cells(9, 8, Total), WateringPattern.Cells(9, 8, Total, PairDirection.Across));
            CollectionAssert.AreEqual(new[] { 6 }, WateringPattern.Cells(6, 1, Total, PairDirection.Across));
        }

        [Test]
        public void Four_IsWholeBed()
        {
            CollectionAssert.AreEqual(new[] { 4, 5, 6, 7 }, WateringPattern.Cells(6, 4, Total));
            CollectionAssert.AreEqual(new[] { 16, 17, 18, 19 }, WateringPattern.Cells(19, 4, Total), "다음 구획 첫 밭");
        }

        [Test]
        public void Eight_IsNeighborBedInSameRow()
        {
            CollectionAssert.AreEqual(new[] { 8, 9, 10, 11, 12, 13, 14, 15 }, WateringPattern.Cells(9, 8, Total));
        }

        [Test]
        public void Eight_MatchesLayout_SameRowAdjacentBeds()
        {
            // 같은 줄(across가 같은 두 밭) — 이웃 밭은 길 건너편이 아니라 옆
            var cells = WateringPattern.Cells(0, 8, Total);
            double across0 = FarmLayout.CellOffset(0, 16, 2, 5.6, 11.2).Across;
            double across4 = FarmLayout.CellOffset(4, 16, 2, 5.6, 11.2).Across;
            Assert.AreEqual(across0, across4, 1e-9);
            CollectionAssert.Contains(cells, 4);
        }

        [Test]
        public void NonPowerOfTwo_RoundsDown_OutOfRangeCut()
        {
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, WateringPattern.Cells(3, 3, Total), "3칸 → 2칸");
            CollectionAssert.AreEqual(new[] { 16, 17, 18, 19 }, WateringPattern.Cells(17, 8, 20), "열린 칸 20개 밖은 뺌");
            CollectionAssert.IsEmpty(WateringPattern.Cells(40, 1, Total));
        }
    }
}
