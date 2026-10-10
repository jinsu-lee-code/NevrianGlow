using NevrianGlowFarm.Logic.Inventory;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class SlotInventoryTests
    {
        // 도구는 칸당 1개, 그 외 10개 (BALANCE 2-4)
        private static int MaxStack(string id) => id.StartsWith("tool_") ? 1 : 10;

        private static SlotInventory Make(int slots) => new SlotInventory(slots, MaxStack);

        [Test]
        public void Add_FillsSameStackFirst_ThenEmptySlots()
        {
            var inv = Make(4);
            inv.TryPlace(2, "crop_moonroot", 8);
            var all = new[] { inv };

            Assert.IsTrue(InventoryRules.TryAddAll(all, "crop_moonroot", 5));

            Assert.AreEqual(10, inv[2].Count, "같은 묶음을 먼저 채움");
            Assert.AreEqual("crop_moonroot", inv[0].ItemId, "남은 3개는 첫 빈 칸");
            Assert.AreEqual(3, inv[0].Count);
            Assert.IsTrue(inv[1].IsEmpty);
        }

        [Test]
        public void Add_SameStackAcrossContainers_BeforeEmptyInFirst()
        {
            var hotbar = Make(2);
            var bag = Make(2);
            bag.TryPlace(1, "crop_moonroot", 9);

            Assert.IsTrue(InventoryRules.TryAddAll(new[] { hotbar, bag }, "crop_moonroot", 1));

            Assert.AreEqual(10, bag[1].Count, "가방의 같은 묶음 → 빈 핫바보다 먼저");
            Assert.IsTrue(hotbar[0].IsEmpty);
        }

        [Test]
        public void Add_EmptySlots_InContainerOrder()
        {
            var hotbar = Make(1);
            var bag = Make(2);
            hotbar.TryPlace(0, "tool_hoe_1", 1);

            Assert.IsTrue(InventoryRules.TryAddAll(new[] { hotbar, bag }, "crop_moonroot", 2));

            Assert.AreEqual(2, bag[0].Count, "핫바가 차면 가방 첫 빈 칸");
        }

        [Test]
        public void NotEnoughSpace_AddsNothing()
        {
            var inv = Make(2);
            inv.TryPlace(0, "tool_hoe_1", 1);
            inv.TryPlace(1, "crop_moonroot", 9);
            var all = new[] { inv };

            Assert.IsFalse(InventoryRules.CanAddAll(all, "crop_moonroot", 2));
            Assert.IsFalse(InventoryRules.TryAddAll(all, "crop_moonroot", 2));
            Assert.AreEqual(9, inv[1].Count, "일부만 넣지 않음");
            Assert.IsTrue(InventoryRules.TryAddAll(all, "crop_moonroot", 1));
        }

        [Test]
        public void Tools_StackOnePerSlot()
        {
            var inv = Make(3);
            var all = new[] { inv };

            Assert.IsTrue(InventoryRules.TryAddAll(all, "tool_hoe_1", 2));

            Assert.AreEqual(1, inv[0].Count);
            Assert.AreEqual(1, inv[1].Count);
            Assert.IsFalse(inv.TryPlace(2, "tool_hoe_1", 2), "칸당 최대 초과");
        }

        [Test]
        public void RemoveAt_ToZero_EmptiesSlot_NotEnoughFails()
        {
            var inv = Make(1);
            inv.TryPlace(0, "item_seed_moonroot", 2);

            Assert.IsFalse(inv.TryRemoveAt(0, 3));
            Assert.IsTrue(inv.TryRemoveAt(0, 1));
            Assert.AreEqual(1, inv[0].Count);
            Assert.IsTrue(inv.TryRemoveAt(0, 1));
            Assert.IsTrue(inv[0].IsEmpty);
            Assert.IsFalse(inv.TryRemoveAt(0, 1));
        }

        [Test]
        public void Resize_GrowKeepsContents_ShrinkDropsTail()
        {
            var inv = Make(2);
            inv.TryPlace(1, "crop_moonroot", 4);

            inv.Resize(4);
            Assert.AreEqual(4, inv.SlotCount);
            Assert.AreEqual(4, inv[1].Count);

            inv.Resize(1);
            Assert.AreEqual(1, inv.SlotCount);
            Assert.IsTrue(inv[1].IsEmpty, "범위 밖은 빈 칸으로 읽힘");
        }
    }
}
