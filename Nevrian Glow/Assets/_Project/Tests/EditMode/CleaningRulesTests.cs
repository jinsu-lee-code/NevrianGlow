using NevrianGlowFarm.Logic;
using NevrianGlowFarm.Logic.Cleaning;
using NevrianGlowFarm.Logic.Inventory;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>BALANCE 2-5 (개수) · 3-7 (청결도) · 배치 간격.</summary>
    public class CleaningRulesTests
    {
        [Test]
        public void TrashCount_Base6PlusDay_Cap12()
        {
            int[] expected = { 7, 8, 9, 10, 11, 12, 12, 12 }; // 1~8일차
            for (int day = 1; day <= expected.Length; day++)
                Assert.AreEqual(expected[day - 1], CleaningRules.DailyCount(6, day, 1, 6), $"{day}일차");
        }

        [Test]
        public void StainCount_Base4PlusHalfDayFloor_Cap8()
        {
            int[] expected = { 4, 5, 5, 6, 6, 7, 7, 8, 8, 8 }; // 1~10일차
            for (int day = 1; day <= expected.Length; day++)
                Assert.AreEqual(expected[day - 1], CleaningRules.DailyCount(4, day, 2, 4), $"{day}일차");
        }

        // BALANCE 3-7: 1일차 쓰레기 7 · 얼룩 4 = 처음 11
        [TestCase(0, 0.0, 1.0, TestName = "모두 치움")]
        [TestCase(7, 4.0, 0.0, TestName = "아무것도 안 함")]
        [TestCase(2, 4.0, 0.4545, TestName = "5개 주워 들고 있음, 얼룩 그대로")]
        [TestCase(2, 3.0, 0.5455, TestName = "2개 다시 내려놓음, 얼룩 2개 반만")]
        public void Cleanliness_Balance37(int floorTrash, double stainSum, double expected)
        {
            Assert.AreEqual(expected, CleaningRules.Cleanliness(7, 4, floorTrash, stainSum), 1e-4);
        }

        [Test]
        public void Cleanliness_CarryOverCountsInInitial_ZeroInitialIsClean()
        {
            Assert.AreEqual(1.0, CleaningRules.Cleanliness(8 + 3, 5, 0, 0), 1e-9, "이월 3개 포함 16개 모두 치움");
            Assert.AreEqual(1.0 - 3.0 / 16.0, CleaningRules.Cleanliness(11, 5, 3, 0), 1e-9, "이월분만 남김");
            Assert.AreEqual(1.0, CleaningRules.Cleanliness(0, 0, 0, 0), 1e-9);
            Assert.AreEqual(0.0, CleaningRules.Cleanliness(1, 0, 5, 0), 1e-9, "0 아래로 안 내려감");
        }

        [Test]
        public void Scatter_RespectsSpacingAreaAndAllowed()
        {
            var random = new SeededRandom(42);
            var occupied = new[] { new FloorPoint(0, 0) };
            var points = CleaningRules.Scatter(12, -5, -4, 5, 3, 0.8, 30, random, p => !(p.X > 3 && p.Z < 0), occupied);

            Assert.AreEqual(12, points.Count, "넓은 곳이라 다 놓임");
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                Assert.IsTrue(p.X >= -5 && p.X <= 5 && p.Z >= -4 && p.Z <= 3, "구역 안");
                Assert.IsFalse(p.X > 3 && p.Z < 0, "금지 구역 밖");
                Assert.GreaterOrEqual(p.DistanceTo(occupied[0]), 0.8, "바닥 아이템과 간격");
                for (int j = i + 1; j < points.Count; j++)
                    Assert.GreaterOrEqual(p.DistanceTo(points[j]), 0.8, "서로 간격");
            }
        }

        [Test]
        public void Scatter_SameSeedSameResult_TooSmallAreaPlacesFewer()
        {
            var a = CleaningRules.Scatter(8, 0, 0, 4, 4, 0.8, 30, new SeededRandom(7));
            var b = CleaningRules.Scatter(8, 0, 0, 4, 4, 0.8, 30, new SeededRandom(7));
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].X, b[i].X);
                Assert.AreEqual(a[i].Z, b[i].Z);
            }

            var crowded = CleaningRules.Scatter(20, 0, 0, 1, 1, 0.8, 30, new SeededRandom(1));
            Assert.Less(crowded.Count, 20, "1 m 사각형에 0.8 m 간격 20개는 못 놓음");
            Assert.GreaterOrEqual(crowded.Count, 1);
        }

        [Test]
        public void Wipe_StopsAtZero()
        {
            Assert.AreEqual(0.1, CleaningRules.Wipe(1.0, 0.9), 1e-9);
            Assert.AreEqual(0.0, CleaningRules.Wipe(0.1, 0.9), 1e-9);
        }

        [Test]
        public void RemoveAll_EmptiesEveryStackOfThatItem()
        {
            SlotInventory Make(int n) => new SlotInventory(n, id => id.StartsWith("tool_") ? 1 : 10);
            var hotbar = Make(2);
            var bag = Make(2);
            hotbar.TryPlace(0, "item_trash", 10);
            hotbar.TryPlace(1, "tool_mop_1", 1);
            bag.TryPlace(1, "item_trash", 3);
            var all = new[] { hotbar, bag };

            Assert.AreEqual(13, InventoryRules.CountOf(all, "item_trash"));
            Assert.AreEqual(13, InventoryRules.RemoveAll(all, "item_trash"));
            Assert.IsTrue(hotbar[0].IsEmpty);
            Assert.IsTrue(bag[1].IsEmpty);
            Assert.AreEqual("tool_mop_1", hotbar[1].ItemId);
        }
    }
}
