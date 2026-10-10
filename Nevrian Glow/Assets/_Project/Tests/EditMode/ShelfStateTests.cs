using NevrianGlowFarm.Logic.Shop;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>BALANCE 2-4: 진열대 기본 6칸 × 칸당 4개, 칸마다 한 종류.</summary>
    public class ShelfStateTests
    {
        [Test]
        public void EmptySlot_AcceptsAny_FillsToFour()
        {
            var shelf = new ShelfState(6, 4);

            for (int i = 0; i < 4; i++)
                Assert.IsTrue(shelf.TryPlace(0, "crop_moonroot"));
            Assert.AreEqual(PlaceCheck.Full, shelf.CanPlace(0, "crop_moonroot"));
            Assert.IsFalse(shelf.TryPlace(0, "crop_moonroot"));
            Assert.AreEqual(4, shelf[0].Count);
        }

        [Test]
        public void OneKindPerSlot()
        {
            var shelf = new ShelfState(6, 4);
            shelf.TryPlace(2, "crop_moonroot");

            Assert.AreEqual(PlaceCheck.OtherItem, shelf.CanPlace(2, "crop_mushroomflower"));
            Assert.IsTrue(shelf.TryPlace(3, "crop_mushroomflower"), "옆 칸은 다른 작물 가능");
        }

        [Test]
        public void TakeOne_FromLast_EmptySlotFreesKind()
        {
            var shelf = new ShelfState(6, 4);
            shelf.TryPlace(1, "crop_moonroot");
            shelf.TryPlace(1, "crop_moonroot");

            Assert.AreEqual("crop_moonroot", shelf.TakeOne(1));
            Assert.AreEqual(1, shelf[1].Count);
            Assert.AreEqual("crop_moonroot", shelf.TakeOne(1));
            Assert.IsTrue(shelf[1].IsEmpty);
            Assert.IsNull(shelf.TakeOne(1));
            Assert.AreEqual(PlaceCheck.Ok, shelf.CanPlace(1, "crop_lanternfruit"), "빈 칸은 다시 아무 작물");
        }

        [Test]
        public void TotalCount_AndCapacity24()
        {
            var shelf = new ShelfState(6, 4);
            for (int slot = 0; slot < 6; slot++)
                for (int i = 0; i < 5; i++)
                    shelf.TryPlace(slot, "crop_moonroot");

            Assert.AreEqual(24, shelf.TotalCount);
        }

        [Test]
        public void Expand_AddsSlots_CannotShrink_BadSlotRejected()
        {
            var shelf = new ShelfState(6, 4);
            shelf.SetSlotCount(8);

            Assert.AreEqual(8, shelf.SlotCount);
            Assert.AreEqual(PlaceCheck.NoSlot, shelf.CanPlace(8, "crop_moonroot"));
            Assert.AreEqual(PlaceCheck.NoSlot, shelf.CanPlace(-1, "crop_moonroot"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => shelf.SetSlotCount(6));
        }

        [Test]
        public void Layout_CenteredSlots_NearestWithinSnap()
        {
            // 6칸 · 1 m 간격: 칸 중심 −2.5 … 2.5
            Assert.AreEqual(-2.5, ShelfLayout.SlotCenterX(0, 6, 1.0), 1e-9);
            Assert.AreEqual(2.5, ShelfLayout.SlotCenterX(5, 6, 1.0), 1e-9);

            Assert.AreEqual(0, ShelfLayout.NearestSlot(-2.4, 6, 1.0, 0.6));
            Assert.AreEqual(3, ShelfLayout.NearestSlot(0.9, 6, 1.0, 0.6));
            Assert.AreEqual(-1, ShelfLayout.NearestSlot(-3.4, 6, 1.0, 0.6), "끝 칸 바깥");
            Assert.AreEqual(2, ShelfLayout.NearestSlot(-0.05, 6, 1.0, 0.6), "칸 경계 근처는 가까운 쪽");
            Assert.AreEqual(-1, ShelfLayout.NearestSlot(0.0, 6, 1.0, 0.4), "snap보다 멀면 없음");
        }
    }
}
