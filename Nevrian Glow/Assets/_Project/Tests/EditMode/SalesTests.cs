using System.Collections.Generic;
using NevrianGlowFarm.Logic;
using NevrianGlowFarm.Logic.Sales;
using NevrianGlowFarm.Logic.Shop;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>BALANCE 2-6 · 3-4 손님 수, 생성 시각, 재고 가중 구매, 재고 0 즉시 퇴장, 건너뛰기 = 실시간과 같은 결과.</summary>
    public class SalesTests
    {
        // BALANCE 2-6 기본값
        private static readonly SalesSpec Spec = new SalesSpec(8, 2, 24, 0.6, 30, 0.8, 6, 1, 3);
        private static readonly Dictionary<string, int> Prices = new Dictionary<string, int> { { "a", 15 }, { "b", 30 } };
        private static int PriceOf(string id) => Prices[id];

        private static ShelfState Shelf(params (int Slot, string Id, int Count)[] items)
        {
            var shelf = new ShelfState(6, 4);
            foreach (var (slot, id, count) in items)
                for (int i = 0; i < count; i++)
                    shelf.TryPlace(slot, id);
            return shelf;
        }

        [TestCase(1, 8), TestCase(2, 9), TestCase(3, 9), TestCase(4, 10), TestCase(5, 10), TestCase(6, 11)]
        public void CustomerCount_Clean100_MatchesBalance34(int day, int expected)
        {
            Assert.AreEqual(expected, SalesRules.CustomerCount(day, 0, 1, Spec));
        }

        [TestCase(1, 5), TestCase(2, 5), TestCase(3, 5), TestCase(4, 6), TestCase(5, 6), TestCase(6, 7)]
        public void CustomerCount_Clean0_IsTimes06(int day, int expected)
        {
            Assert.AreEqual(expected, SalesRules.CustomerCount(day, 0, 0, Spec));
        }

        [Test]
        public void CustomerCount_CapAndBonusAndHalfRoundsUp()
        {
            Assert.AreEqual(24, SalesRules.CustomerCount(100, 0, 1, Spec), "상한 24 (보너스 · 청결 전)");
            Assert.AreEqual(12, SalesRules.CustomerCount(4, 0.2, 1, Spec), "10 × 1.2");
            Assert.AreEqual(9, SalesRules.CustomerCount(1, 0.0625, 1, Spec), "8 × 1.0625 = 8.5 → 9 (짝수 반올림 아님)");
            Assert.AreEqual(9, SalesRules.CustomerCount(2, 0, 2, Spec), "청결도 1 초과는 1로");
        }

        [Test]
        public void SpawnTime_EvenlyInsideWindow()
        {
            // 10명, 30초 × 0.8 = 24초 → 2.4초 간격, 첫 손님 0초, 마지막 21.6초
            Assert.AreEqual(0.0, SalesRules.SpawnTime(0, 10, Spec), 1e-9);
            Assert.AreEqual(2.4, SalesRules.SpawnTime(1, 10, Spec), 1e-9);
            Assert.AreEqual(21.6, SalesRules.SpawnTime(9, 10, Spec), 1e-9);
        }

        [Test]
        public void SpawnDue_RespectsScheduleAndMaxConcurrent()
        {
            var session = new SalesSession(10, Spec);
            Assert.AreEqual(1, session.SpawnDue(0), "0초: 첫 손님");
            Assert.AreEqual(0, session.SpawnDue(2.3));
            Assert.AreEqual(1, session.SpawnDue(2.4));
            Assert.AreEqual(4, session.SpawnDue(12.1), "4.8 · 7.2 · 9.6 · 12.0 → 6명까지");
            Assert.AreEqual(6, session.InStore);
            Assert.AreEqual(0, session.SpawnDue(30), "가득 참 → 미룸");
            session.Leave();
            Assert.AreEqual(1, session.SpawnDue(30), "자리가 나면 늦게 들어옴");
        }

        [Test]
        public void EmptyShelf_LeavesImmediately_NoRevenue()
        {
            var shelves = new List<ShelfState> { new ShelfState(6, 4) };
            var basket = SalesRules.FillBasket(shelves, new SeededRandom(1), Spec);
            Assert.IsEmpty(basket);
            var session = new SalesSession(3, Spec);
            session.SkipRemaining(shelves, new SeededRandom(1), PriceOf);
            Assert.AreEqual(3, session.EmptyHanded);
            Assert.AreEqual(0, session.Buyers);
            Assert.AreEqual(0, session.Revenue);
            Assert.IsTrue(session.IsFinished);
        }

        [Test]
        public void Basket_BuysOneToThree_StopsWhenStockRunsOut()
        {
            var random = new SeededRandom(7);
            for (int n = 0; n < 200; n++)
            {
                var shelves = new List<ShelfState> { Shelf((0, "a", 4), (1, "b", 4)) };
                int count = SalesRules.FillBasket(shelves, random, Spec).Count;
                Assert.That(count, Is.InRange(1, 3));
                Assert.AreEqual(8 - count, shelves[0].TotalCount);
            }
            var one = new List<ShelfState> { Shelf((2, "a", 1)) };
            var basket = SalesRules.FillBasket(one, new SeededRandom(3), new SalesSpec(8, 2, 24, 0.6, 30, 0.8, 6, 3, 3));
            CollectionAssert.AreEqual(new[] { "a" }, basket, "3개 원해도 1개뿐이면 1개");
        }

        [Test]
        public void Pick_IsWeightedByStock()
        {
            // a 3개 · b 1개 (진열대 두 개에 나눠 놓음) → a가 뽑힐 확률 75%
            var shelves = new List<ShelfState> { Shelf((0, "a", 3)), Shelf((5, "b", 1)) };
            var random = new SeededRandom(20261011);
            int a = 0;
            const int trials = 4000;
            for (int n = 0; n < trials; n++)
            {
                Assert.IsTrue(SalesRules.TryPickUnit(shelves, random, out int shelf, out int slot));
                if (shelves[shelf][slot].ItemId == "a")
                    a++;
            }
            Assert.AreEqual(0.75, (double)a / trials, 0.03);
        }

        [Test]
        public void Pay_AddsRevenueAtCheckout()
        {
            var session = new SalesSession(2, Spec);
            Assert.AreEqual(60, session.Pay(new[] { "a", "a", "b" }, PriceOf));
            Assert.AreEqual(0, session.Pay(new string[0], PriceOf));
            Assert.AreEqual(60, session.Revenue);
            Assert.AreEqual(3, session.ItemsSold);
            Assert.AreEqual(1, session.Buyers);
            Assert.AreEqual(1, session.EmptyHanded);
            Assert.AreEqual(2, session.Sold["a"]);
            Assert.AreEqual(1, session.Sold["b"]);
        }

        [Test]
        public void Skip_SameResultAsRealtime_SameSeed()
        {
            // 실시간: 손님이 한 명씩 들어와 사고 계산하고 나감 / 건너뛰기: 처음부터 같은 함수로 즉시
            var realtimeShelves = new List<ShelfState> { Shelf((0, "a", 4), (1, "b", 4), (2, "a", 2)) };
            var realtime = new SalesSession(9, Spec);
            var random1 = new SeededRandom(42);
            for (double t = 0; !realtime.IsFinished; t += 0.1)
            {
                int due = realtime.SpawnDue(t);
                for (int i = 0; i < due; i++)
                {
                    realtime.Pay(SalesRules.FillBasket(realtimeShelves, random1, Spec), PriceOf);
                    realtime.Leave();
                }
            }

            var skipShelves = new List<ShelfState> { Shelf((0, "a", 4), (1, "b", 4), (2, "a", 2)) };
            var skip = new SalesSession(9, Spec);
            Assert.AreEqual(9, skip.SkipRemaining(skipShelves, new SeededRandom(42), PriceOf));

            Assert.AreEqual(realtime.Revenue, skip.Revenue);
            Assert.AreEqual(realtime.ItemsSold, skip.ItemsSold);
            Assert.AreEqual(realtime.Buyers, skip.Buyers);
            Assert.AreEqual(realtime.EmptyHanded, skip.EmptyHanded);
            Assert.AreEqual(realtimeShelves[0].TotalCount, skipShelves[0].TotalCount);
            Assert.IsTrue(skip.IsFinished);
        }

        [Test]
        public void Skip_MidDay_OnlyRemainingCustomers()
        {
            var shelves = new List<ShelfState> { Shelf((0, "a", 4), (1, "a", 4), (2, "b", 4)) };
            var session = new SalesSession(5, Spec);
            var random = new SeededRandom(5);
            int due = session.SpawnDue(5.0); // 0 · 4.8초 → 2명
            Assert.AreEqual(2, due);
            for (int i = 0; i < due; i++)
            {
                session.Pay(SalesRules.FillBasket(shelves, random, Spec), PriceOf);
                session.Leave();
            }
            Assert.AreEqual(3, session.SkipRemaining(shelves, random, PriceOf));
            Assert.AreEqual(5, session.Buyers + session.EmptyHanded);
            Assert.AreEqual(12 - shelves[0].TotalCount, session.ItemsSold);
        }

        [Test]
        public void Take_UsesCallback_LastPlacedFirst()
        {
            var shelves = new List<ShelfState> { Shelf((0, "a", 2)) };
            var taken = new List<(int, int)>();
            var basket = SalesRules.FillBasket(shelves, new SeededRandom(1), new SalesSpec(8, 2, 24, 0.6, 30, 0.8, 6, 2, 2),
                (s, slot) => { taken.Add((s, slot)); return shelves[s].TakeOne(slot); });
            Assert.AreEqual(2, basket.Count);
            CollectionAssert.AreEqual(new[] { (0, 0), (0, 0) }, taken);
            Assert.AreEqual(0, shelves[0].TotalCount);
        }
    }
}
