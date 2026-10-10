using NevrianGlowFarm.Logic;
using NevrianGlowFarm.Logic.Sales;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>3-2 계산대 줄 순서 · 손님 종류(등장 일차 · 가중치).</summary>
    public class CustomerLogicTests
    {
        [Test]
        public void Line_FirstComeFirstServed_MovesUp()
        {
            var line = new CheckoutLine();
            Assert.AreEqual(0, line.Join(7));
            Assert.AreEqual(1, line.Join(3));
            Assert.AreEqual(2, line.Join(9));
            Assert.AreEqual(1, line.Join(3), "이미 서 있으면 그 자리");
            Assert.IsTrue(line.IsFront(7));
            Assert.IsFalse(line.IsFront(3));

            Assert.IsTrue(line.Leave(7));
            Assert.IsTrue(line.IsFront(3));
            Assert.AreEqual(0, line.PositionOf(3));
            Assert.AreEqual(1, line.PositionOf(9), "한 칸 앞으로");
            Assert.AreEqual(-1, line.PositionOf(7));
            Assert.IsFalse(line.Leave(7));
        }

        [Test]
        public void Line_LeaveFromMiddle_BackMovesUp()
        {
            var line = new CheckoutLine();
            line.Join(1);
            line.Join(2);
            line.Join(3);
            line.Leave(2);
            Assert.AreEqual(1, line.PositionOf(3));
            Assert.AreEqual(2, line.Count);
        }

        [Test]
        public void CustomerType_RespectsUnlockDay()
        {
            var weights = new double[] { 1, 1, 1 };
            var days = new[] { 1, 3, 1 };
            var random = new SeededRandom(11);
            for (int i = 0; i < 500; i++)
                Assert.AreNotEqual(1, SalesRules.PickCustomerType(weights, days, 2, random), "3일차 손님은 2일차에 안 나옴");
            bool seen = false;
            for (int i = 0; i < 500 && !seen; i++)
                seen = SalesRules.PickCustomerType(weights, days, 3, random) == 1;
            Assert.IsTrue(seen, "3일차부터 나옴");
        }

        [Test]
        public void CustomerType_WeightedAndNoneWhenEmpty()
        {
            var weights = new double[] { 3, 1, 0 };
            var days = new[] { 1, 1, 1 };
            var random = new SeededRandom(20261011);
            int first = 0;
            const int trials = 4000;
            for (int i = 0; i < trials; i++)
            {
                int pick = SalesRules.PickCustomerType(weights, days, 1, random);
                Assert.AreNotEqual(2, pick, "가중치 0은 안 나옴");
                if (pick == 0)
                    first++;
            }
            Assert.AreEqual(0.75, (double)first / trials, 0.03);
            Assert.AreEqual(-1, SalesRules.PickCustomerType(new double[] { 1 }, new[] { 5 }, 1, random), "나올 종류가 없으면 -1");
            Assert.AreEqual(-1, SalesRules.PickCustomerType(new double[0], new int[0], 1, random));
        }
    }
}
