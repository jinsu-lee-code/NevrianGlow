using NevrianGlowFarm.Logic.Game;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>BALANCE 2-7 · 3-2 (v1): 레벨업 필요량 = 80 + 40 × (레벨 − 1) = 기본 40 + 40 × 레벨, SP +3.</summary>
    public class ProgressionTests
    {
        private static readonly LevelCurve Curve = new LevelCurve(1.0, 40, 40, 3);

        [Test]
        public void Table_MatchesBalance32()
        {
            var rows = Progression.Table(Curve, 6);
            int[] toNext = { 80, 120, 160, 200, 240, 280 };
            int[] cumulative = { 0, 80, 200, 360, 560, 800 };
            int[] sp = { 0, 3, 6, 9, 12, 15 };
            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(i + 1, rows[i].Level);
                Assert.AreEqual(toNext[i], rows[i].ToNext, $"Lv{i + 1} 다음까지");
                Assert.AreEqual(cumulative[i], rows[i].CumulativeExp, $"Lv{i + 1} 누적");
                Assert.AreEqual(sp[i], rows[i].CumulativeSkillPoints, $"Lv{i + 1} SP");
            }
        }

        [Test]
        public void ExactlyRequired_LevelsUp_ExpResetsToZero()
        {
            var p = new Progression(Curve);

            var gain = p.AddExp(80);

            Assert.AreEqual(2, p.Level);
            Assert.AreEqual(0, p.Exp);
            Assert.AreEqual(120, p.Required);
            Assert.AreEqual(1, gain.LevelsGained);
            Assert.AreEqual(3, gain.SkillPointsGained);
        }

        [Test]
        public void BigGain_LevelsUpRepeatedly_LeftoverCarries()
        {
            var p = new Progression(Curve);

            var gain = p.AddExp(250); // 80 + 120 = 200 → Lv3, 남은 50

            Assert.AreEqual(3, p.Level);
            Assert.AreEqual(50, p.Exp);
            Assert.AreEqual(2, gain.LevelsGained);
            Assert.AreEqual(6, gain.SkillPointsGained);
        }

        [Test]
        public void SmallGains_Accumulate()
        {
            var p = new Progression(Curve);

            Assert.IsFalse(p.AddExp(30).LeveledUp);
            Assert.IsFalse(p.AddExp(49).LeveledUp);
            Assert.AreEqual(79, p.Exp);
            Assert.IsTrue(p.AddExp(1).LeveledUp);
            Assert.AreEqual(2, p.Level);
        }

        [Test]
        public void Revenue_IsExp_ZeroOrNegativeIgnored()
        {
            var p = new Progression(Curve);

            Assert.AreEqual(150, p.AddRevenue(150).Exp, "경험치 = 매출 × 1");
            Assert.AreEqual(2, p.Level);
            Assert.AreEqual(70, p.Exp);
            Assert.AreEqual(0, p.AddRevenue(0).Exp);
            Assert.AreEqual(0, p.AddExp(-5).Exp);
            Assert.AreEqual(70, p.Exp);
        }

        [Test]
        public void ExpPerRevenue_FloorsFractions()
        {
            var half = new LevelCurve(0.5, 40, 40, 3);

            Assert.AreEqual(7, half.ExpFromRevenue(15), "7.5 → 7");
        }

        [Test]
        public void LoadState_ContinuesFromLevel()
        {
            var p = new Progression(Curve, 4, 190);

            Assert.AreEqual(200, p.Required);
            var gain = p.AddExp(10);
            Assert.AreEqual(5, p.Level);
            Assert.AreEqual(0, p.Exp);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Progression(Curve, 0, 0));
        }
    }
}
