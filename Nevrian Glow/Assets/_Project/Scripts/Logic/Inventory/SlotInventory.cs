using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Inventory
{
    /// <summary>칸 하나의 내용. 빈 칸은 ItemId가 null.</summary>
    public readonly struct ItemStack
    {
        public string ItemId { get; }
        public int Count { get; }
        public bool IsEmpty => ItemId == null || Count <= 0;

        public ItemStack(string itemId, int count)
        {
            ItemId = count > 0 ? itemId : null;
            Count = itemId != null && count > 0 ? count : 0;
        }

        public static readonly ItemStack Empty = default;
    }

    /// <summary>
    /// 칸 묶음 (핫바 · 가방, GDD 4-3). 아이템은 id로만 다룬다(엔진 의존 없음). 칸당 최대 개수는 maxStackOf가 알려 준다.
    /// 넣기 순서 규칙은 InventoryRules (여러 칸 묶음을 이어서 쓴다: 같은 묶음 → 빈 핫바 → 빈 가방).
    /// </summary>
    public sealed class SlotInventory
    {
        private readonly List<ItemStack> slots;
        private readonly Func<string, int> maxStackOf;

        public SlotInventory(int slotCount, Func<string, int> maxStackOf)
        {
            if (slotCount < 0) throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "0 이상");
            this.maxStackOf = maxStackOf ?? throw new ArgumentNullException(nameof(maxStackOf));
            slots = new List<ItemStack>(slotCount);
            for (int i = 0; i < slotCount; i++)
                slots.Add(ItemStack.Empty);
        }

        public int SlotCount => slots.Count;

        public ItemStack this[int index] => index >= 0 && index < slots.Count ? slots[index] : ItemStack.Empty;

        /// <summary>id의 칸당 최대 개수 (1 이상).</summary>
        public int MaxStackOf(string itemId) => Math.Max(1, maxStackOf(itemId));

        /// <summary>빈 칸에 바로 놓는다 (시작 소지품 · 불러오기). 칸이 차 있거나 개수가 칸당 최대를 넘으면 false.</summary>
        public bool TryPlace(int index, string itemId, int count)
        {
            if (index < 0 || index >= slots.Count || !slots[index].IsEmpty || itemId == null || count <= 0 || count > MaxStackOf(itemId))
                return false;
            slots[index] = new ItemStack(itemId, count);
            return true;
        }

        /// <summary>한 칸에서 개수만큼 뺀다. 모자라면 false. 0이 되면 빈 칸.</summary>
        public bool TryRemoveAt(int index, int count)
        {
            if (index < 0 || index >= slots.Count || count <= 0)
                return false;
            var stack = slots[index];
            if (stack.IsEmpty || stack.Count < count)
                return false;
            slots[index] = new ItemStack(stack.ItemId, stack.Count - count);
            return true;
        }

        /// <summary>칸 수를 바꾼다. 줄이면 없어진 칸의 내용은 사라진다 (게임에서는 늘리기만).</summary>
        public void Resize(int slotCount)
        {
            if (slotCount < 0) throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "0 이상");
            while (slots.Count < slotCount)
                slots.Add(ItemStack.Empty);
            if (slots.Count > slotCount)
                slots.RemoveRange(slotCount, slots.Count - slotCount);
        }

        /// <summary>
        /// 칸 수와 내용을 통째로 정한다 (불러오기 6-1). 칸당 최대를 넘는 개수는 최대로 자르고, 그만큼 잘린 개수를 돌려준다.
        /// </summary>
        public int Load(IReadOnlyList<ItemStack> stacks)
        {
            int clipped = 0;
            Resize(stacks != null ? stacks.Count : 0);
            for (int i = 0; i < slots.Count; i++)
            {
                var s = stacks[i];
                if (s.IsEmpty)
                {
                    slots[i] = ItemStack.Empty;
                    continue;
                }
                int max = MaxStackOf(s.ItemId);
                clipped += Math.Max(0, s.Count - max);
                slots[i] = new ItemStack(s.ItemId, Math.Min(s.Count, max));
            }
            return clipped;
        }

        // InventoryRules 전용: 검사 없이 칸을 바꾼다.
        internal void Set(int index, ItemStack stack) => slots[index] = stack;
    }

    /// <summary>
    /// 여러 칸 묶음에 아이템을 넣는 규칙 (GDD 4-3): 먼저 모든 묶음의 같은 아이템 칸을 채우고, 그다음 묶음 순서대로 빈 칸.
    /// 전부 넣을 수 있을 때만 넣는다 (넣을 칸이 없으면 수확 · 줍기를 하지 않음).
    /// </summary>
    public static class InventoryRules
    {
        /// <summary>count개를 전부 넣을 자리가 있는지.</summary>
        public static bool CanAddAll(IReadOnlyList<SlotInventory> containers, string itemId, int count)
        {
            if (itemId == null || count <= 0)
                return false;
            return FreeSpace(containers, itemId, count) >= count;
        }

        /// <summary>count개를 전부 넣는다. 자리가 모자라면 아무것도 넣지 않고 false.</summary>
        public static bool TryAddAll(IReadOnlyList<SlotInventory> containers, string itemId, int count)
        {
            if (!CanAddAll(containers, itemId, count))
                return false;

            int left = count;
            // 1) 같은 아이템 칸 채우기
            foreach (var inv in containers)
            {
                int max = inv.MaxStackOf(itemId);
                for (int i = 0; i < inv.SlotCount && left > 0; i++)
                {
                    var s = inv[i];
                    if (s.IsEmpty || s.ItemId != itemId || s.Count >= max)
                        continue;
                    int put = Math.Min(max - s.Count, left);
                    inv.Set(i, new ItemStack(itemId, s.Count + put));
                    left -= put;
                }
            }
            // 2) 묶음 순서대로 빈 칸
            foreach (var inv in containers)
            {
                int max = inv.MaxStackOf(itemId);
                for (int i = 0; i < inv.SlotCount && left > 0; i++)
                {
                    if (!inv[i].IsEmpty)
                        continue;
                    int put = Math.Min(max, left);
                    inv.Set(i, new ItemStack(itemId, put));
                    left -= put;
                }
            }
            return true;
        }

        /// <summary>모든 묶음에서 그 아이템을 전부 뺀다 (쓰레기통 비우기). 뺀 개수.</summary>
        public static int RemoveAll(IReadOnlyList<SlotInventory> containers, string itemId)
        {
            int removed = 0;
            foreach (var inv in containers)
                for (int i = 0; i < inv.SlotCount; i++)
                {
                    var s = inv[i];
                    if (s.IsEmpty || s.ItemId != itemId)
                        continue;
                    removed += s.Count;
                    inv.Set(i, ItemStack.Empty);
                }
            return removed;
        }

        /// <summary>
        /// 빈 칸 하나를 다른 묶음들에 있는 같은 아이템의 첫 묶음으로 채운다 (핫바 씨앗을 다 쓰면 가방에서 내려옴).
        /// 옮긴 개수 — 칸이 차 있거나 같은 아이템이 없으면 0.
        /// </summary>
        public static int RefillFrom(SlotInventory target, int index, string itemId, IReadOnlyList<SlotInventory> sources)
        {
            if (target == null || itemId == null || index < 0 || index >= target.SlotCount || !target[index].IsEmpty)
                return 0;
            foreach (var inv in sources)
                for (int i = 0; i < inv.SlotCount; i++)
                {
                    var s = inv[i];
                    if (s.IsEmpty || s.ItemId != itemId)
                        continue;
                    int move = Math.Min(s.Count, target.MaxStackOf(itemId));
                    target.Set(index, new ItemStack(itemId, move));
                    inv.Set(i, new ItemStack(itemId, s.Count - move));
                    return move;
                }
            return 0;
        }

        /// <summary>모든 묶음에 있는 그 아이템 개수.</summary>
        public static int CountOf(IReadOnlyList<SlotInventory> containers, string itemId)
        {
            int count = 0;
            foreach (var inv in containers)
                for (int i = 0; i < inv.SlotCount; i++)
                    if (!inv[i].IsEmpty && inv[i].ItemId == itemId)
                        count += inv[i].Count;
            return count;
        }

        private static int FreeSpace(IReadOnlyList<SlotInventory> containers, string itemId, int enough)
        {
            int free = 0;
            foreach (var inv in containers)
            {
                int max = inv.MaxStackOf(itemId);
                for (int i = 0; i < inv.SlotCount && free < enough; i++)
                {
                    var s = inv[i];
                    if (s.IsEmpty)
                        free += max;
                    else if (s.ItemId == itemId && s.Count < max)
                        free += max - s.Count;
                }
            }
            return free;
        }
    }
}
