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
    }
}
