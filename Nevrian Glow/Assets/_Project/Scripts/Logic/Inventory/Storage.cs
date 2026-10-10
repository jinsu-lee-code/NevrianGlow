using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Inventory
{
    /// <summary>
    /// 창고 (GDD 4-3): 하나뿐(농장 문 옆 · 가게 창고 구역이 같은 보관함), 용량 무제한, 상하지 않음, 보관 불가 아이템(쓰레기) 거부.
    /// 아이템 종류별 개수만 가진다. 목록 순서 = 처음 들어온 순서. 엔진 의존 없음.
    /// </summary>
    public sealed class Storage
    {
        private readonly List<string> order = new List<string>();
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        private readonly Func<string, bool> canStore;

        public Storage(Func<string, bool> canStore)
        {
            this.canStore = canStore ?? throw new ArgumentNullException(nameof(canStore));
        }

        /// <summary>종류별 묶음 (개수 &gt; 0만, 들어온 순서).</summary>
        public IReadOnlyList<ItemStack> Entries
        {
            get
            {
                var list = new List<ItemStack>(order.Count);
                foreach (var id in order)
                    list.Add(new ItemStack(id, counts[id]));
                return list;
            }
        }

        public int KindCount => order.Count;
        public bool CanStore(string itemId) => itemId != null && canStore(itemId);
        public int CountOf(string itemId) => itemId != null && counts.TryGetValue(itemId, out int n) ? n : 0;

        /// <summary>넣는다. 보관 불가 아이템이거나 개수가 0 이하면 false.</summary>
        public bool TryDeposit(string itemId, int count)
        {
            if (count <= 0 || !CanStore(itemId))
                return false;
            if (counts.TryGetValue(itemId, out int have))
                counts[itemId] = have + count;
            else
            {
                counts[itemId] = count;
                order.Add(itemId);
            }
            return true;
        }

        /// <summary>꺼낸다. 모자라면 false. 0이 되면 목록에서 빠진다.</summary>
        public bool TryWithdraw(string itemId, int count)
        {
            if (count <= 0 || CountOf(itemId) < count)
                return false;
            int left = counts[itemId] - count;
            if (left > 0)
                counts[itemId] = left;
            else
            {
                counts.Remove(itemId);
                order.Remove(itemId);
            }
            return true;
        }
    }

    /// <summary>창고 ↔ 인벤토리 옮기기 규칙 (넣기 · 전부 넣기 · 종류별 꺼내기).</summary>
    public static class StorageRules
    {
        /// <summary>칸 하나의 묶음을 통째로 넣는다. 빈 칸 · 보관 불가면 false.</summary>
        public static bool DepositSlot(Storage storage, SlotInventory inventory, int index)
        {
            var stack = inventory[index];
            if (stack.IsEmpty || !storage.CanStore(stack.ItemId))
                return false;
            storage.TryDeposit(stack.ItemId, stack.Count);
            inventory.Set(index, ItemStack.Empty);
            return true;
        }

        /// <summary>include가 참인 보관 가능 묶음을 모두 넣는다 (예: 도구 제외). 넣은 칸 수를 돌려준다.</summary>
        public static int DepositAll(Storage storage, IReadOnlyList<SlotInventory> containers, Func<string, bool> include)
        {
            int moved = 0;
            foreach (var inv in containers)
                for (int i = 0; i < inv.SlotCount; i++)
                {
                    var s = inv[i];
                    if (!s.IsEmpty && (include == null || include(s.ItemId)) && DepositSlot(storage, inv, i))
                        moved++;
                }
            return moved;
        }

        /// <summary>
        /// 한 종류를 한 묶음(칸당 최대)만큼 꺼내 인벤토리에 넣는다. 자리가 모자라면 들어가는 만큼만.
        /// 꺼낸 개수를 돌려준다 (0 = 자리 없음 또는 창고에 없음).
        /// </summary>
        public static int WithdrawStack(Storage storage, IReadOnlyList<SlotInventory> containers, string itemId)
        {
            if (containers.Count == 0)
                return 0;
            int amount = Math.Min(storage.CountOf(itemId), containers[0].MaxStackOf(itemId));
            while (amount > 0 && !InventoryRules.CanAddAll(containers, itemId, amount))
                amount--;
            if (amount <= 0)
                return 0;
            storage.TryWithdraw(itemId, amount);
            InventoryRules.TryAddAll(containers, itemId, amount);
            return amount;
        }
    }
}
