using NevrianGlowFarm.Logic.Game;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class DayCycleTests
    {
        [Test]
        public void NewGame_StartsDayOneShop()
        {
            var cycle = new DayCycle();

            Assert.AreEqual(1, cycle.Day);
            Assert.AreEqual(DayPhase.Shop, cycle.Phase);
        }

        [Test]
        public void Advance_FollowsDailyOrder()
        {
            var cycle = new DayCycle();
            var expected = new[] { DayPhase.Farming, DayPhase.CleanDisplay, DayPhase.Sales, DayPhase.Settlement, DayPhase.Skill };

            foreach (var phase in expected)
            {
                var change = cycle.Advance();
                Assert.AreEqual(phase, cycle.Phase);
                Assert.AreEqual(1, cycle.Day);
                Assert.IsFalse(change.DayChanged);
            }
        }

        [Test]
        public void AdvanceAfterSkill_GoesToNextDayShop()
        {
            var cycle = new DayCycle(1, DayPhase.Skill);

            var change = cycle.Advance();

            Assert.AreEqual(2, cycle.Day);
            Assert.AreEqual(DayPhase.Shop, cycle.Phase);
            Assert.IsTrue(change.DayChanged);
            Assert.AreEqual(1, change.PreviousDay);
            Assert.AreEqual(DayPhase.Skill, change.PreviousPhase);
        }

        [Test]
        public void SixFullDays_EndOnDaySevenShop_DayChangesSixTimes()
        {
            var cycle = new DayCycle();
            int dayChanges = 0;

            for (int i = 0; i < DayCycle.Order.Count * 6; i++)
                if (cycle.Advance().DayChanged)
                    dayChanges++;

            Assert.AreEqual(7, cycle.Day);
            Assert.AreEqual(DayPhase.Shop, cycle.Phase);
            Assert.AreEqual(6, dayChanges);
        }

        [Test]
        public void AdvanceToNextDay_SkipsRemainingPhases()
        {
            var cycle = new DayCycle(3, DayPhase.Farming);

            var change = cycle.AdvanceToNextDay();

            Assert.AreEqual(4, cycle.Day);
            Assert.AreEqual(DayPhase.Shop, cycle.Phase);
            Assert.IsTrue(change.DayChanged);
        }

        [Test]
        public void TrySetPhase_ChangesWithinSameDay_SamePhaseIsNotAChange()
        {
            var cycle = new DayCycle(2, DayPhase.Shop);

            Assert.IsTrue(cycle.TrySetPhase(DayPhase.Sales, out var change));
            Assert.AreEqual(DayPhase.Sales, cycle.Phase);
            Assert.AreEqual(2, cycle.Day);
            Assert.IsFalse(change.DayChanged);

            Assert.IsFalse(cycle.TrySetPhase(DayPhase.Sales, out _));
        }

        [Test]
        public void InvalidStart_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new DayCycle(0, DayPhase.Shop));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new DayCycle(1, (DayPhase)99));
        }
    }
}
