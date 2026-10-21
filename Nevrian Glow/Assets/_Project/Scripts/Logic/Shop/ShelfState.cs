using System;
using System.Collections.Generic;
using NevrianGlowFarm.Logic.Inventory;

namespace NevrianGlowFarm.Logic.Shop
{
    /// <summary>진열 칸에 놓을 수 있는지와 이유.</summary>
    public enum PlaceCheck
    {
        Ok,
        NoSlot,
        OtherItem,
        Full,
    }

    /// <summary>
    /// 진열대 (GDD 4-3, BALANCE 2-4): 칸마다 한 종류 · 칸당 최대 개수. 놓기 · 회수 · 판매는 한 개씩, 빼는 것은 마지막에 놓은 것부터.
    /// 엔진 의존 없음 — 장사 시뮬레이터(3-1) · 측정 봇 · 저장이 같은 상태를 쓴다.
    /// </summary>
    public sealed class ShelfState
    {
        private readonly List<ItemStack> slots = new List<ItemStack>();

        public ShelfState(int slotCount, int itemsPerSlot)
        {
            if (itemsPerSlot < 1) throw new ArgumentOutOfRangeException(nameof(itemsPerSlot), itemsPerSlot, "1 이상");
            ItemsPerSlot = itemsPerSlot;
            SetSlotCount(slotCount);
        }

        public int ItemsPerSlot { get; }
        public int SlotCount => slots.Count;
        public ItemStack this[int slot] => slot >= 0 && slot < slots.Count ? slots[slot] : ItemStack.Empty;

        /// <summary>진열된 전체 개수.</summary>
        public int TotalCount
        {
            get
            {
                int n = 0;
                foreach (var s in slots)
                    n += s.Count;
                return n;
            }
        }

        public PlaceCheck CanPlace(int slot, string itemId)
        {
            if (slot < 0 || slot >= slots.Count || itemId == null)
                return PlaceCheck.NoSlot;
            var s = slots[slot];
            if (s.IsEmpty)
                return PlaceCheck.Ok;
            if (s.ItemId != itemId)
                return PlaceCheck.OtherItem;
            return s.Count >= ItemsPerSlot ? PlaceCheck.Full : PlaceCheck.Ok;
        }

        /// <summary>1개 놓는다. 놓을 수 없으면 false.</summary>
        public bool TryPlace(int slot, string itemId)
        {
            if (CanPlace(slot, itemId) != PlaceCheck.Ok)
                return false;
            var s = slots[slot];
            slots[slot] = new ItemStack(itemId, s.Count + 1);
            return true;
        }

        /// <summary>1개 뺀다 (회수 · 판매 — 마지막에 놓은 것부터). 뺀 아이템 id, 비었으면 null.</summary>
        public string TakeOne(int slot)
        {
            if (slot < 0 || slot >= slots.Count || slots[slot].IsEmpty)
                return null;
            var s = slots[slot];
            slots[slot] = new ItemStack(s.ItemId, s.Count - 1);
            return s.ItemId;
        }

        /// <summary>칸 수와 내용을 통째로 정한다 (불러오기 6-1). 칸당 개수를 넘으면 자른다.</summary>
        public void Load(IReadOnlyList<ItemStack> stacks)
        {
            slots.Clear();
            if (stacks == null)
                return;
            foreach (var s in stacks)
                slots.Add(s.IsEmpty ? ItemStack.Empty : new ItemStack(s.ItemId, Math.Min(s.Count, ItemsPerSlot)));
        }

        /// <summary>칸 수를 늘린다 (진열대 확장 상품 3-3). 줄이지 않는다.</summary>
        public void SetSlotCount(int slotCount)
        {
            if (slotCount < slots.Count)
                throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "진열 칸은 줄일 수 없음");
            while (slots.Count < slotCount)
                slots.Add(ItemStack.Empty);
        }
    }

    /// <summary>진열대 칸 배치: 칸은 진열대 가로(로컬 x)로 같은 간격, 가운데 정렬.</summary>
    public static class ShelfLayout
    {
        /// <summary>칸 중심의 로컬 x.</summary>
        public static double SlotCenterX(int slot, int slotCount, double slotPitch) => (slot - (slotCount - 1) * 0.5) * slotPitch;

        /// <summary>로컬 x에서 가장 가까운 칸. 그 칸 중심에서 snap보다 멀면 −1.</summary>
        public static int NearestSlot(double localX, int slotCount, double slotPitch, double snap)
        {
            if (slotCount <= 0 || slotPitch <= 0)
                return -1;
            int slot = (int)Math.Round(localX / slotPitch + (slotCount - 1) * 0.5);
            if (slot < 0 || slot >= slotCount)
                return -1;
            return Math.Abs(localX - SlotCenterX(slot, slotCount, slotPitch)) <= snap ? slot : -1;
        }
    }
}
