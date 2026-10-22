using System.Collections.Generic;
using NevrianGlowFarm.Logic;
using NevrianGlowFarm.Logic.Cleaning;
using NevrianGlowFarm.Logic.Sales;
using NevrianGlowFarm.Logic.Shop;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>P-3 가게 정리 (청소 v2 설정안 · game_settings.csv): 생기는 양, 정돈도, 등급, 흐트러진 칸, 구매 확률 절반.</summary>
    public class TidyRulesTests
    {
        // game_settings.csv 기획값
        private static readonly MessSpec Spec = new MessSpec(30, 12, 1, 3, 0.3, 0.15, 3, 0.20, 0.95, 0.80, 0.60);

        [Test]
        public void Day1_BigCleanOnly()
        {
            var random = new SeededRandom(1);
            Assert.AreEqual(30, TidyRules.TrashCount(1, Spec, random));
            Assert.AreEqual(12, TidyRules.StainCount(1, 0, 1, Spec));
            Assert.AreEqual(0, TidyRules.ScatterCount(1, 10, 5, 1, Spec), "1일차는 어제 장사가 없음");
            Assert.AreEqual(0, TidyRules.DisorderCount(1, 5, 1, Spec));
        }

        [Test]
        public void LaterDays_SmallTrash_StainsFromCustomers()
        {
            var random = new SeededRandom(7);
            for (int i = 0; i < 50; i++)
            {
                int t = TidyRules.TrashCount(2, Spec, random);
                Assert.That(t, Is.InRange(1, 3), "평소 쓰레기 1~3개");
            }
            Assert.AreEqual(3, TidyRules.StainCount(3, 10, 1, Spec), "손님 10 × 0.3");
            Assert.AreEqual(2, TidyRules.StainCount(3, 5, 1, Spec), "5 × 0.3 = 1.5 → 2 (0.5 올림)");
            Assert.AreEqual(2, TidyRules.StainCount(3, 10, 0.5, Spec), "청결 유지 5단계(× 0.5): 1.5 → 2");
        }

        [Test]
        public void Scatter_UnsoldAndCustomers()
        {
            // 안 팔린 10 × 0.15 + 손님 6 ÷ 3 = 1.5 + 2 = 3.5 → 4
            Assert.AreEqual(4, TidyRules.ScatterCount(3, 10, 6, 1, Spec));
            Assert.AreEqual(1, TidyRules.ScatterCount(2, 0, 3, 1, Spec), "다 팔려도 손님 3명당 1개");
            Assert.AreEqual(0, TidyRules.ScatterCount(2, 0, 1, 1, Spec), "1 ÷ 3 = 0.33 → 0");
            Assert.AreEqual(2, TidyRules.ScatterCount(3, 10, 6, 0.5, Spec), "청결 유지 × 0.5: 1.75 → 2");
        }

        [Test]
        public void Disorder_TwentyPercentOfFilledSlots()
        {
            Assert.AreEqual(1, TidyRules.DisorderCount(2, 5, 1, Spec), "5칸 × 0.2 = 1");
            Assert.AreEqual(1, TidyRules.DisorderCount(2, 3, 1, Spec), "0.6 → 1");
            Assert.AreEqual(0, TidyRules.DisorderCount(2, 2, 1, Spec), "0.4 → 0");
            Assert.AreEqual(0, TidyRules.DisorderCount(2, 0, 1, Spec));
        }

        [Test]
        public void Tidiness_AndGrades()
        {
            Assert.AreEqual(1.0, TidyRules.Tidiness(0, 0, 0, 0), 1e-9, "어지러움이 없으면 1");
            Assert.AreEqual(0.5, TidyRules.Tidiness(3, 1, 2, 0), 1e-9, "4개 중 바닥 2개 남음");
            Assert.AreEqual(0.0, TidyRules.Tidiness(3, 1, 5, 1), 1e-9, "더 흩뜨려도 0 아래로 안 감");
            Assert.AreEqual(TidyGrade.S, TidyRules.Grade(0.95, Spec));
            Assert.AreEqual(TidyGrade.A, TidyRules.Grade(0.949, Spec));
            Assert.AreEqual(TidyGrade.A, TidyRules.Grade(0.80, Spec));
            Assert.AreEqual(TidyGrade.B, TidyRules.Grade(0.60, Spec));
            Assert.AreEqual(TidyGrade.C, TidyRules.Grade(0.59, Spec));
        }

        [Test]
        public void ShelfDisorder_FlagsStraightenAndEmpty()
        {
            var shelf = new ShelfState(3, 4);
            shelf.TryPlace(0, "a");
            shelf.TryPlace(1, "b");
            shelf.SetDisordered(2);
            Assert.IsFalse(shelf.IsDisordered(2), "빈 칸은 흐트러지지 않음");
            shelf.SetDisordered(0);
            Assert.IsTrue(shelf.IsDisordered(0));
            Assert.AreEqual(1, shelf.DisorderedCount);
            Assert.IsTrue(shelf.Straighten(0));
            Assert.AreEqual(0, shelf.DisorderedCount);
            shelf.SetDisordered(1);
            shelf.TakeOne(1);
            Assert.AreEqual(0, shelf.DisorderedCount, "다 빠진 칸은 흐트러짐도 없어짐");

            var picked = TidyRules.PickDisorderSlots(new List<ShelfState> { shelf }, 5, new SeededRandom(3));
            CollectionAssert.AreEqual(new[] { (0, 0) }, picked, "상품이 있는 칸만, 많아도 있는 만큼");
        }

        [Test]
        public void DisorderedSlot_HalfBuyChance()
        {
            // 칸 0(흐트러짐) a 4개, 칸 1 b 4개 → 무게 2 : 4 → a가 뽑힐 확률 1/3
            var shelf = new ShelfState(2, 4);
            for (int i = 0; i < 4; i++) { shelf.TryPlace(0, "a"); shelf.TryPlace(1, "b"); }
            shelf.SetDisordered(0);
            var shelves = new List<ShelfState> { shelf };
            var random = new SeededRandom(11);
            int a = 0, n = 3000;
            for (int k = 0; k < n; k++)
                if (SalesRules.TryPickUnit(shelves, random, out _, out int slot, 0.5) && slot == 0)
                    a++;
            Assert.AreEqual(1.0 / 3.0, (double)a / n, 0.03);

            // 배율 1이면 예전 그대로 반반
            int a2 = 0;
            for (int k = 0; k < n; k++)
                if (SalesRules.TryPickUnit(shelves, random, out _, out int slot2) && slot2 == 0)
                    a2++;
            Assert.AreEqual(0.5, (double)a2 / n, 0.03);
        }
    }
}
