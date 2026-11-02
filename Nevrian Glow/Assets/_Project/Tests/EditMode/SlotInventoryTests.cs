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
        public void Refill_EmptiedHotbarSlot_TakesFirstSameStackFromBag()
        {
            // 핫바 씨앗 1개를 다 쓰면 가방의 같은 씨앗 첫 묶음이 그 칸으로 (2026-10-30 사용자 요청)
            var hotbar = Make(2);
            var bag = Make(3);
            hotbar.TryPlace(1, "item_seed_moonroot", 1);
            bag.TryPlace(0, "item_seed_glowcap", 10);
            bag.TryPlace(1, "item_seed_moonroot", 10);
            bag.TryPlace(2, "item_seed_moonroot", 4);

            Assert.IsTrue(hotbar.TryRemoveAt(1, 1));
            int moved = InventoryRules.RefillFrom(hotbar, 1, "item_seed_moonroot", new[] { bag });

            Assert.AreEqual(10, moved);
            Assert.AreEqual("item_seed_moonroot", hotbar[1].ItemId, "같은 칸에 같은 씨앗");
            Assert.AreEqual(10, hotbar[1].Count, "가방 첫 묶음 통째로");
            Assert.IsTrue(bag[1].IsEmpty, "옮긴 가방 칸은 빔");
            Assert.AreEqual(4, bag[2].Count, "다음 묶음은 그대로");
            Assert.AreEqual("item_seed_glowcap", bag[0].ItemId, "다른 씨앗은 내려오지 않음");
        }

        [Test]
        public void Refill_DoesNothing_WhenSlotNotEmptyOrNoSameItem()
        {
            var hotbar = Make(1);
            var bag = Make(1);
            hotbar.TryPlace(0, "item_seed_moonroot", 3);
            bag.TryPlace(0, "item_seed_moonroot", 10);

            Assert.AreEqual(0, InventoryRules.RefillFrom(hotbar, 0, "item_seed_moonroot", new[] { bag }), "칸에 아직 남아 있으면 채우지 않음");
            Assert.AreEqual(3, hotbar[0].Count);

            Assert.IsTrue(hotbar.TryRemoveAt(0, 3));
            Assert.AreEqual(0, InventoryRules.RefillFrom(hotbar, 0, "item_seed_glowcap", new[] { bag }), "가방에 같은 아이템이 없으면 빈 칸 그대로");
            Assert.IsTrue(hotbar[0].IsEmpty);
            Assert.AreEqual(10, bag[0].Count);
        }

        [Test]
        public void Refill_ByKind_NextSeedComesDown()
        {
            var hotbar = Make(1);
            var bag = Make(3);
            bag.TryPlace(0, "crop_moonroot", 5);
            bag.TryPlace(1, "item_seed_mushroomflower", 6);
            bag.TryPlace(2, "item_seed_moonroot", 2);

            int moved = InventoryRules.RefillFrom(hotbar, 0, id => id.StartsWith("item_seed_"), new[] { bag });

            Assert.AreEqual(6, moved);
            Assert.AreEqual("item_seed_mushroomflower", hotbar[0].ItemId, "가방에서 처음 만나는 같은 종류");
            Assert.AreEqual("crop_moonroot", bag[0].ItemId, "다른 종류는 내려오지 않음");
            Assert.IsTrue(bag[1].IsEmpty);
            Assert.AreEqual(2, bag[2].Count);
        }

        // 기획 튜토리얼 11/02 1일차: 씨앗을 산 뒤 수확 → 핫바가 도구 3 + 씨앗으로 차 있어 작물은 가방에
        private static SlotInventory HotbarWithTools(string fourthItem, int count)
        {
            var hotbar = Make(4);
            hotbar.TryPlace(0, "tool_hoe_1", 1);
            hotbar.TryPlace(1, "tool_can_1", 1);
            hotbar.TryPlace(2, "tool_mop_1", 1);
            if (fourthItem != null)
                hotbar.TryPlace(3, fourthItem, count);
            return hotbar;
        }

        private static bool IsCrop(string id) => id.StartsWith("crop_");
        private static bool NotToolOrCrop(string id) => !id.StartsWith("tool_") && !IsCrop(id);

        [Test]
        public void BringDown_EmptySlotFirst()
        {
            var hotbar = HotbarWithTools(null, 0);
            var bag = Make(4);
            bag.TryPlace(1, "crop_moonroot", 6);

            Assert.AreEqual(3, InventoryRules.BringDown(hotbar, bag, IsCrop, NotToolOrCrop));
            Assert.AreEqual("crop_moonroot", hotbar[3].ItemId);
            Assert.AreEqual(6, hotbar[3].Count);
            Assert.IsTrue(bag[1].IsEmpty, "빈 칸과 맞바꿈 = 가방 칸이 빔");
        }

        [Test]
        public void BringDown_SwapsWithLeftoverSeeds_WhenNoEmptySlot()
        {
            // 씨앗 10개를 사서 8칸만 심음 → 2개가 핫바에 남아 있다
            var hotbar = HotbarWithTools("item_seed_moonroot", 2);
            var bag = Make(4);
            bag.TryPlace(0, "crop_moonroot", 6);

            Assert.AreEqual(3, InventoryRules.BringDown(hotbar, bag, IsCrop, NotToolOrCrop));
            Assert.AreEqual("crop_moonroot", hotbar[3].ItemId);
            Assert.AreEqual("item_seed_moonroot", bag[0].ItemId, "씨앗은 작물이 있던 가방 칸으로");
            Assert.AreEqual(2, bag[0].Count);
        }

        [Test]
        public void BringDown_AlreadyInHotbar_ChangesNothing()
        {
            var hotbar = HotbarWithTools("crop_moonroot", 3);
            var bag = Make(1);
            bag.TryPlace(0, "crop_lanternfruit", 5);

            Assert.AreEqual(3, InventoryRules.BringDown(hotbar, bag, IsCrop, NotToolOrCrop));
            Assert.AreEqual("crop_moonroot", hotbar[3].ItemId);
            Assert.AreEqual("crop_lanternfruit", bag[0].ItemId);
        }

        [Test]
        public void BringDown_NeverReplacesTools()
        {
            var hotbar = Make(2);
            hotbar.TryPlace(0, "tool_hoe_1", 1);
            hotbar.TryPlace(1, "tool_can_1", 1);
            var bag = Make(1);
            bag.TryPlace(0, "crop_moonroot", 6);

            Assert.AreEqual(-1, InventoryRules.BringDown(hotbar, bag, IsCrop, NotToolOrCrop), "자리가 없으면 그대로");
            Assert.AreEqual("tool_hoe_1", hotbar[0].ItemId);
            Assert.AreEqual(6, bag[0].Count);
            Assert.AreEqual(-1, InventoryRules.BringDown(hotbar, Make(1), IsCrop, NotToolOrCrop), "작물이 없으면 -1");
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
