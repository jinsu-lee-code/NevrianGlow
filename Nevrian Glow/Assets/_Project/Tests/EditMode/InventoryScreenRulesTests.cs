using NevrianGlowFarm.Logic.Inventory;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>가방 칸 클릭 (집기 · 놓기 · 바꾸기 · 합치기) · 창고 (넣기 · 전부 넣기 · 꺼내기 · 보관 불가).</summary>
    public class InventoryScreenRulesTests
    {
        private static int MaxStack(string id) => id.StartsWith("tool_") ? 1 : 10;
        private static SlotInventory Make(int slots) => new SlotInventory(slots, MaxStack);
        private static bool CanStore(string id) => id != "item_trash";

        // ── 칸 클릭 ──

        [Test]
        public void EmptyHand_PicksUpWholeStack_ThenPlacesInEmptySlot()
        {
            var inv = Make(3);
            inv.TryPlace(0, "crop_moonroot", 7);
            var cursor = ItemStack.Empty;

            Assert.IsTrue(CursorRules.Click(inv, 0, ref cursor));
            Assert.AreEqual(7, cursor.Count);
            Assert.IsTrue(inv[0].IsEmpty);

            Assert.IsTrue(CursorRules.Click(inv, 2, ref cursor));
            Assert.IsTrue(cursor.IsEmpty);
            Assert.AreEqual(7, inv[2].Count);
        }

        [Test]
        public void SameItem_MergesUpToMax_RestStaysInHand()
        {
            var inv = Make(2);
            inv.TryPlace(0, "crop_moonroot", 6);
            inv.TryPlace(1, "crop_moonroot", 8);
            var cursor = ItemStack.Empty;
            CursorRules.Click(inv, 0, ref cursor);

            Assert.IsTrue(CursorRules.Click(inv, 1, ref cursor));

            Assert.AreEqual(10, inv[1].Count);
            Assert.AreEqual(4, cursor.Count, "6 − 2 = 4개는 손에 남음");
        }

        [Test]
        public void DifferentItem_Swaps()
        {
            var inv = Make(2);
            inv.TryPlace(0, "tool_hoe_1", 1);
            inv.TryPlace(1, "crop_moonroot", 3);
            var cursor = ItemStack.Empty;
            CursorRules.Click(inv, 0, ref cursor);

            Assert.IsTrue(CursorRules.Click(inv, 1, ref cursor));

            Assert.AreEqual("tool_hoe_1", inv[1].ItemId);
            Assert.AreEqual("crop_moonroot", cursor.ItemId);
            Assert.AreEqual(3, cursor.Count);
        }

        [Test]
        public void EmptyHandOnEmptySlot_OrFullSameStack_IsNotAChange()
        {
            var inv = Make(2);
            inv.TryPlace(1, "crop_moonroot", 10);
            var cursor = ItemStack.Empty;

            Assert.IsFalse(CursorRules.Click(inv, 0, ref cursor));

            cursor = new ItemStack("crop_moonroot", 2);
            Assert.IsFalse(CursorRules.Click(inv, 1, ref cursor));
            Assert.AreEqual(2, cursor.Count);
        }

        [Test]
        public void ReturnCursor_GoesBackBySameRules()
        {
            var hotbar = Make(1);
            var bag = Make(2);
            hotbar.TryPlace(0, "tool_hoe_1", 1);
            bag.TryPlace(1, "crop_moonroot", 9);
            var cursor = new ItemStack("crop_moonroot", 3);

            Assert.IsTrue(CursorRules.ReturnCursor(new[] { hotbar, bag }, ref cursor));

            Assert.IsTrue(cursor.IsEmpty);
            Assert.AreEqual(10, bag[1].Count);
            Assert.AreEqual(2, bag[0].Count);
        }

        // ── 창고 ──

        [Test]
        public void Storage_Unlimited_KeepsFirstInOrder()
        {
            var storage = new Storage(CanStore);

            Assert.IsTrue(storage.TryDeposit("crop_moonroot", 500));
            Assert.IsTrue(storage.TryDeposit("tool_mop_1", 1));
            Assert.IsTrue(storage.TryDeposit("crop_moonroot", 5));

            Assert.AreEqual(505, storage.CountOf("crop_moonroot"));
            Assert.AreEqual("crop_moonroot", storage.Entries[0].ItemId);
            Assert.AreEqual("tool_mop_1", storage.Entries[1].ItemId);
        }

        [Test]
        public void Storage_RejectsTrash()
        {
            var storage = new Storage(CanStore);
            var inv = Make(1);
            inv.TryPlace(0, "item_trash", 3);

            Assert.IsFalse(storage.TryDeposit("item_trash", 1));
            Assert.IsFalse(StorageRules.DepositSlot(storage, inv, 0));
            Assert.AreEqual(3, inv[0].Count, "쓰레기는 인벤토리에 그대로");
            Assert.AreEqual(0, storage.KindCount);
        }

        [Test]
        public void Withdraw_ToZero_RemovesEntry_NotEnoughFails()
        {
            var storage = new Storage(CanStore);
            storage.TryDeposit("crop_moonroot", 3);

            Assert.IsFalse(storage.TryWithdraw("crop_moonroot", 4));
            Assert.IsTrue(storage.TryWithdraw("crop_moonroot", 3));
            Assert.AreEqual(0, storage.KindCount);
        }

        [Test]
        public void DepositAll_SkipsExcludedAndTrash()
        {
            var storage = new Storage(CanStore);
            var hotbar = Make(3);
            var bag = Make(2);
            hotbar.TryPlace(0, "tool_hoe_1", 1);
            hotbar.TryPlace(1, "crop_moonroot", 4);
            bag.TryPlace(0, "item_trash", 2);
            bag.TryPlace(1, "item_seed_moonroot", 6);

            int moved = StorageRules.DepositAll(storage, new[] { hotbar, bag }, id => !id.StartsWith("tool_"));

            Assert.AreEqual(2, moved);
            Assert.AreEqual("tool_hoe_1", hotbar[0].ItemId, "도구는 남김");
            Assert.AreEqual("item_trash", bag[0].ItemId, "쓰레기는 남김");
            Assert.AreEqual(4, storage.CountOf("crop_moonroot"));
            Assert.AreEqual(6, storage.CountOf("item_seed_moonroot"));
        }

        [Test]
        public void WithdrawStack_TakesOneStack_OrWhatFits()
        {
            var storage = new Storage(CanStore);
            storage.TryDeposit("crop_moonroot", 25);
            var inv = Make(2);
            inv.TryPlace(0, "tool_hoe_1", 1);
            var all = new[] { inv };

            Assert.AreEqual(10, StorageRules.WithdrawStack(storage, all, "crop_moonroot"), "한 묶음 = 10");
            Assert.AreEqual(15, storage.CountOf("crop_moonroot"));
            Assert.AreEqual(0, StorageRules.WithdrawStack(storage, all, "crop_moonroot"), "자리 없음");

            inv.TryRemoveAt(1, 3);
            Assert.AreEqual(3, StorageRules.WithdrawStack(storage, all, "crop_moonroot"), "들어가는 만큼만");
            Assert.AreEqual(12, storage.CountOf("crop_moonroot"));
        }
    }
}
