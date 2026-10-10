using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Inventory
{
    /// <summary>
    /// 가방 화면에서 칸을 클릭할 때의 규칙 (GDD 4-3: 칸 클릭으로 집기 · 놓기 · 바꾸기 · 합치기).
    /// 마우스에 든 묶음(cursor)과 칸 하나 사이에서만 움직인다. 엔진 의존 없음.
    /// </summary>
    public static class CursorRules
    {
        /// <summary>
        /// 칸 클릭 1회.
        /// 손이 비었으면 칸 묶음을 집는다 / 칸이 비었으면 놓는다 / 같은 아이템이면 칸이 찰 때까지 합치고 남은 것은 손에 /
        /// 다른 아이템이면 바꾼다. 바뀐 것이 없으면 false (빈 손으로 빈 칸 · 가득 찬 같은 묶음).
        /// </summary>
        public static bool Click(SlotInventory inventory, int index, ref ItemStack cursor)
        {
            if (inventory == null || index < 0 || index >= inventory.SlotCount)
                return false;
            var slot = inventory[index];

            if (cursor.IsEmpty)
            {
                if (slot.IsEmpty)
                    return false;
                cursor = slot;
                inventory.Set(index, ItemStack.Empty);
                return true;
            }

            if (slot.IsEmpty)
            {
                inventory.Set(index, cursor);
                cursor = ItemStack.Empty;
                return true;
            }

            if (slot.ItemId == cursor.ItemId)
            {
                int max = inventory.MaxStackOf(slot.ItemId);
                int put = Math.Min(max - slot.Count, cursor.Count);
                if (put <= 0)
                    return false;
                inventory.Set(index, new ItemStack(slot.ItemId, slot.Count + put));
                cursor = new ItemStack(cursor.ItemId, cursor.Count - put);
                return true;
            }

            inventory.Set(index, cursor);
            cursor = slot;
            return true;
        }

        /// <summary>화면을 닫을 때 손에 든 묶음을 인벤토리로 돌려놓는다 (같은 묶음 → 빈 칸). 자리가 없으면 false, 손에 그대로.</summary>
        public static bool ReturnCursor(IReadOnlyList<SlotInventory> containers, ref ItemStack cursor)
        {
            if (cursor.IsEmpty)
                return true;
            if (!InventoryRules.TryAddAll(containers, cursor.ItemId, cursor.Count))
                return false;
            cursor = ItemStack.Empty;
            return true;
        }
    }
}
