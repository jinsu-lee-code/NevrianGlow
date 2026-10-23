using System.Collections.Generic;
using NevrianGlowFarm.Logic;
using NevrianGlowFarm.Logic.Sales;
using NevrianGlowFarm.Logic.Save;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>P-2 입소문 손님 모델 (손님수 설정안 · game_settings.csv): 1일차 고정, 확산 · 이탈, 장날 · 상한 · 무작위, 만족도, 첫 손님 2개 · 1개 더, 저장.</summary>
    public class CustomerModelTests
    {
        // game_settings.csv 기획값
        private static readonly CustomerModelSpec Spec = new CustomerModelSpec(1, 2, 2, 40, 0.35, 0.10, 0.5, 7, 1.5, 3, 0.8, 0.35, 0.35, 0.30, 0.10);
        private const int Spots = 8;

        // 매일 만족도 s로 장사한다고 보고 일차별 손님 수 (무작위 가운데 = 0.5, 청결도 1)
        private static List<int> Run(int days, double satisfaction, double adRate = 0.02)
        {
            var model = new CustomerModel();
            var counts = new List<int> { model.TodayCount(1, 1, Spots, 0.5, Spec) };
            model.RecordSales(satisfaction, satisfaction, 1, 1, Spec); // 청결 · 정돈 = s, 다 삼 → S = 0.7s + 0.3
            for (int d = 2; d <= days; d++)
            {
                model.AdvanceNight(d, adRate, Spec);
                int c = model.TodayCount(d, 1, Spots, 0.5, Spec);
                counts.Add(c);
                model.RecordSales(satisfaction, satisfaction, c, c, Spec);
            }
            return counts;
        }

        [Test]
        public void Day1_IsFixedOneCustomer()
        {
            var model = new CustomerModel();
            Assert.AreEqual(1, model.TodayCount(1, 0.1, Spots, 0.0, Spec), "청결 · 무작위와 관계없이 1명");
        }

        [Test]
        public void WellKept_MatchesPlanTable()
        {
            // 설정안 4절 '잘 관리'(S = 1): 1 → 3 → 5 → 6 → 8 → 10, 14일차(장날) 24 상한, 20일차 20.
            // 3일차는 계산값 4.45 → 4 (표 5와 1 차이, ±10% 무작위 폭 안 — TASKS P-2 기록)
            var c = Run(30, 1.0);
            CollectionAssert.AreEqual(new[] { 1, 3, 4, 6, 8, 10 }, c.GetRange(0, 6));
            Assert.AreEqual(18, c[9], "10일차");
            Assert.AreEqual(24, c[13], "14일차 장날 = 상한 8자리 × 3");
            Assert.AreEqual(20, c[19], "20일차 포화");
            Assert.AreEqual(20, c[29]);
        }

        [Test]
        public void Signboard_PullsEarlyGrowth()
        {
            // 설정안 4절: 간판 5단계(p = 0.07) → 1 · 6 · 9 · 12 · 15 · 17 (표와 같음)
            var c = Run(6, 1.0, 0.07);
            CollectionAssert.AreEqual(new[] { 1, 6, 9, 12, 15, 17 }, c);
        }

        [Test]
        public void LowSatisfaction_GrowsSlowerAndLosesRegulars()
        {
            var good = Run(10, 1.0);
            var poor = Run(10, 0.2);
            Assert.Less(poor[9], good[9]);
            var model = new CustomerModel();
            model.Load(5, 20, 0, 0); // 만족도 0 → 입소문 0, 이탈 10%
            model.AdvanceNight(6, 0, Spec);
            Assert.AreEqual(18, model.Regulars, 1e-9, "20 − 0.1 × 1 × 20");
        }

        [Test]
        public void MarketDay_CapAndRandom()
        {
            var model = new CustomerModel();
            model.Load(6, 10, 2, 1);
            Assert.AreEqual(7, model.Expected(6, Spec), 1e-9, "10 × 0.5 + 2");
            model.Load(7, 10, 2, 1);
            Assert.AreEqual(10.5, model.Expected(7, Spec), 1e-9, "7일차 장날 × 1.5");
            Assert.AreEqual(6, model.TodayCount(7, 1, 2, 0.5, Spec), "상한 2자리 × 3");
            model.Load(6, 10, 0, 1);
            Assert.AreEqual(5, model.TodayCount(6, 1, Spots, 0.5, Spec));
            Assert.AreEqual(5, model.TodayCount(6, 1, Spots, 0.0, Spec), "5 × 0.9 = 4.5 → 5 (0.5 올림)");
            Assert.AreEqual(6, model.TodayCount(6, 1, Spots, 1.0, Spec), "5 × 1.1 = 5.5 → 6");
            Assert.AreEqual(4, model.TodayCount(6, 0, Spots, 0.5, Spec), "청결도 0 → × 0.8");
        }

        [Test]
        public void Satisfaction_WeightsCleanTidySuccess()
        {
            // 0.35 × 0.5 + 0.35 × 1 + 0.30 × 3/4
            Assert.AreEqual(0.75, CustomerModel.SatisfactionOf(0.5, 1, 4, 3, Spec), 1e-9);
            Assert.AreEqual(1.0, CustomerModel.SatisfactionOf(1, 1, 0, 0, Spec), 1e-9, "손님이 없으면 구매 성공률 1");
        }

        [Test]
        public void FirstBuyerFixed_ExtraChanceAddsOne()
        {
            var spec = new SalesSpec(30, 0.8, 6, 1, 1);
            var session = new SalesSession(3, spec, firstWanted: 2, extraChance: 1, extraMax: 4);
            var random = new SeededRandom(1);
            Assert.AreEqual(2, session.NextWanted(random), "1일차 첫 손님 2개 고정");
            Assert.AreEqual(2, session.NextWanted(random), "1개 + 확률 100%로 1개 더");
            var plain = new SalesSession(3, spec);
            Assert.AreEqual(1, plain.NextWanted(random));
            Assert.AreEqual(4, SalesRules.WithExtra(4, 1, 4, random), "최대 4개를 넘지 않음");
        }

        [Test]
        public void Save_RoundTripAndOldFile()
        {
            var s = new SaveSnapshot { Day = 3, HasCustomerModel = true, CustomerRegulars = 5.2532, CustomerNewToday = 1.8277, CustomerSatisfaction = 0.85 };
            Assert.IsTrue(SaveFormat.TryRead(SaveFormat.Write(s), out var back, out string error), error);
            Assert.IsTrue(back.HasCustomerModel);
            Assert.AreEqual(5.2532, back.CustomerRegulars, 1e-12);
            Assert.AreEqual(1.8277, back.CustomerNewToday, 1e-12);
            Assert.AreEqual(0.85, back.CustomerSatisfaction, 1e-12);

            Assert.IsTrue(SaveFormat.TryRead(SaveFormat.Write(new SaveSnapshot { Day = 2 }), out var old, out _));
            Assert.IsFalse(old.HasCustomerModel, "P-2 전 파일 = 손님 모델 없음 → 불러올 때 다시 계산");
        }
    }
}
