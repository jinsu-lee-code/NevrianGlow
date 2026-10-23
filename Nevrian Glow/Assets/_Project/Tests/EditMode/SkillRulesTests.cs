using System.Collections.Generic;
using NevrianGlowFarm.Logic.Game;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class SkillRulesTests
    {
        // BALANCE 9-3: 고품질 2/3/4 SP, 등불 열매 6 SP (버섯꽃 1단계 선행)
        private static readonly SkillSpec Quality = new SkillSpec("skill_quality", new[] { 2, 3, 4 });
        private static readonly SkillSpec Lantern = new SkillSpec("skill_unlock_lantern", new[] { 6 }, "skill_unlock_mushroom", 1);

        private static int None(string id) => 0;

        [Test]
        public void NextCost_FollowsLevel_AndZeroAtMax()
        {
            Assert.AreEqual(2, SkillRules.NextCost(Quality, 0));
            Assert.AreEqual(4, SkillRules.NextCost(Quality, 2));
            Assert.AreEqual(0, SkillRules.NextCost(Quality, 3));
        }

        [Test]
        public void CanLearn_ChecksMax_ThenPrerequisite_ThenPoints()
        {
            Assert.AreEqual(LearnCheck.MaxLevel, SkillRules.CanLearn(Quality, 3, None, 99));
            Assert.AreEqual(LearnCheck.Prerequisite, SkillRules.CanLearn(Lantern, 0, None, 99), "선행이 SP보다 먼저");
            Assert.AreEqual(LearnCheck.NotEnoughPoints, SkillRules.CanLearn(Lantern, 0, id => id == "skill_unlock_mushroom" ? 1 : 0, 5));
            Assert.AreEqual(LearnCheck.Ok, SkillRules.CanLearn(Lantern, 0, id => id == "skill_unlock_mushroom" ? 1 : 0, 6));
        }

        [Test]
        public void Prerequisite_NeedsRequiredLevel()
        {
            var fertile = new SkillSpec("skill_fertile", new[] { 10 }, "skill_quality", 2);
            Assert.AreEqual(LearnCheck.Prerequisite, SkillRules.CanLearn(fertile, 0, id => 1, 10));
            Assert.AreEqual(LearnCheck.Ok, SkillRules.CanLearn(fertile, 0, id => 2, 10));
        }

        [Test]
        public void NoCosts_IsMaxedImmediately()
        {
            var empty = new SkillSpec("skill_empty", null);
            Assert.AreEqual(0, empty.MaxLevel);
            Assert.AreEqual(LearnCheck.MaxLevel, SkillRules.CanLearn(empty, 0, None, 99));
        }

        [Test]
        public void Stat_AddsThenMultiplies()
        {
            var mods = new List<StatModifier>
            {
                new StatModifier("stat_sell_price", ModifierOp.Multiply, 1.1),
                new StatModifier("stat_sell_price", ModifierOp.Add, 5),
                new StatModifier("stat_other", ModifierOp.Add, 100),
            };
            // (40 + 5) × 1.1 = 49.5
            Assert.AreEqual(49.5, StatCalculator.Apply("stat_sell_price", 40, mods), 1e-9);
            Assert.AreEqual(50, StatCalculator.ApplyRounded("stat_sell_price", 40, mods), "0.5 올림");
            Assert.AreEqual(40, StatCalculator.ApplyRounded("stat_none", 40, mods), "변경 없는 능력치는 기본값");
        }

        [Test]
        public void Stat_SameStatFromTwoSkills_Stacks()
        {
            // 청결 유지 +0.2 · 간판 +0.1 → 손님 보너스 0.3 (BALANCE 9-3)
            var mods = new List<StatModifier>
            {
                new StatModifier("stat_customer_bonus", ModifierOp.Add, 0.2),
                new StatModifier("stat_customer_bonus", ModifierOp.Add, 0.1),
            };
            Assert.AreEqual(0.3, StatCalculator.Apply("stat_customer_bonus", 0, mods), 1e-9);
        }

        [Test]
        public void QualityPrices_MatchBalance93()
        {
            // 버섯꽃 40달러 × 1.05 / 1.10 / 1.15 → 42 / 44 / 46
            int[] expected = { 42, 44, 46 };
            double[] values = { 1.05, 1.10, 1.15 };
            for (int level = 1; level <= 3; level++)
            {
                Assert.IsTrue(StatCalculator.ValueAtLevel(values, level, out double v));
                var mods = new[] { new StatModifier("stat_sell_price", ModifierOp.Multiply, v) };
                Assert.AreEqual(expected[level - 1], StatCalculator.ApplyRounded("stat_sell_price", 40, mods));
            }
        }

        [Test]
        public void ValueAtLevel_ZeroLevelHasNoEffect_AndClampsToLast()
        {
            double[] values = { 2, 4 };
            Assert.IsFalse(StatCalculator.ValueAtLevel(values, 0, out _));
            Assert.IsTrue(StatCalculator.ValueAtLevel(values, 5, out double v));
            Assert.AreEqual(4, v);
        }

        [Test]
        public void GrowDaysMinusOne_StaysAtLeastOne_ViaCropSpec()
        {
            var mods = new[] { new StatModifier("stat_grow_days", ModifierOp.Add, -1) };
            int moonroot = StatCalculator.ApplyRounded("stat_grow_days", 1, mods);
            var spec = new Logic.Farm.CropSpec(moonroot, 1, 1, 0, 1);
            Assert.AreEqual(1, spec.GrowDays, "월광 1일 → 0이 아니라 최소 1일");
            Assert.AreEqual(7, StatCalculator.ApplyRounded("stat_grow_days", 8, mods), "등불 8 → 7");
        }
    }
}
