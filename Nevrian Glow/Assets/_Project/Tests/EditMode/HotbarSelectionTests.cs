using NevrianGlowFarm.Logic.Player;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class HotbarSelectionTests
    {
        [Test]
        public void KeyNumber_SelectsZeroBasedSlot()
        {
            int selected = 0;

            Assert.IsTrue(HotbarSelection.TrySelectByKeyNumber(3, 5, ref selected));
            Assert.AreEqual(2, selected);
        }

        [Test]
        public void SameSlot_IsNotAChange()
        {
            int selected = 2;

            Assert.IsFalse(HotbarSelection.TrySelectByKeyNumber(3, 5, ref selected));
            Assert.AreEqual(2, selected);
        }

        [Test]
        public void KeyBeyondSlotCount_IsIgnored()
        {
            int selected = 1;

            Assert.IsFalse(HotbarSelection.TrySelectByKeyNumber(6, 5, ref selected));
            Assert.IsFalse(HotbarSelection.TrySelectByKeyNumber(0, 5, ref selected));
            Assert.AreEqual(1, selected);
        }

        [Test]
        public void SlotCount_IsNotFixedToFive()
        {
            int selected = 0;

            Assert.IsTrue(HotbarSelection.TrySelectByKeyNumber(7, 8, ref selected));
            Assert.AreEqual(6, selected);
            Assert.IsFalse(HotbarSelection.TrySelectByKeyNumber(4, 3, ref selected));
        }

        [Test]
        public void Wheel_MovesOneSlotAndWraps()
        {
            int selected = 0;

            Assert.IsTrue(HotbarSelection.TryCycle(1, 4, ref selected));
            Assert.AreEqual(1, selected);
            Assert.IsTrue(HotbarSelection.TryCycle(-1, 4, ref selected));
            Assert.IsTrue(HotbarSelection.TryCycle(-1, 4, ref selected));
            Assert.AreEqual(3, selected, "첫 칸에서 이전 → 마지막 칸");
            Assert.IsTrue(HotbarSelection.TryCycle(1, 4, ref selected));
            Assert.AreEqual(0, selected, "마지막 칸에서 다음 → 첫 칸");
        }

        [Test]
        public void Wheel_StepSizeIsIgnored_OnlyDirectionCounts()
        {
            int selected = 1;

            Assert.IsTrue(HotbarSelection.TryCycle(120, 4, ref selected));
            Assert.AreEqual(2, selected);
            Assert.IsTrue(HotbarSelection.TryCycle(-120, 4, ref selected));
            Assert.AreEqual(1, selected);
        }

        [Test]
        public void Wheel_NoStepOrSingleSlot_IsNotAChange()
        {
            int selected = 0;

            Assert.IsFalse(HotbarSelection.TryCycle(0, 4, ref selected));
            Assert.IsFalse(HotbarSelection.TryCycle(1, 1, ref selected));
            Assert.AreEqual(0, selected);
        }

        [Test]
        public void SlotCountGrows_SelectionKept()
        {
            int selected = 3;

            Assert.IsFalse(HotbarSelection.ClampToSlotCount(6, ref selected));
            Assert.AreEqual(3, selected);
        }

        [Test]
        public void SlotCountShrinksBelowSelection_SelectsLastSlot()
        {
            int selected = 5;

            Assert.IsTrue(HotbarSelection.ClampToSlotCount(4, ref selected));
            Assert.AreEqual(3, selected);
        }
    }
}
