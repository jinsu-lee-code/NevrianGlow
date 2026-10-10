using System;
using System.Collections.Generic;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Inventory;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Storage
{
    /// <summary>
    /// 창고 하나 (GDD 4-3): 농장 문 옆과 가게 창고 구역의 접근 지점(StorageAccessPoint)이 같은 보관함을 연다.
    /// Core 씬에 하나 둔다(구역을 오가도 유지). 규칙은 Logic Storage · StorageRules, 보관 불가는 ItemData.CanStore.
    /// </summary>
    public class StorageSystem : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("아이템 id → 아이템 데이터 (Data/ContentDatabase)")]
        [SerializeField] private ContentDatabase database;
        [Tooltip("시작 창고 내용 (Data/Stats/GameStartSettings)")]
        [SerializeField] private GameStartSettingsData startSettings;

        [Header("디버그")]
        [Tooltip("창고 내용 (읽기 전용)")]
        [SerializeField] private List<string> contentsView = new List<string>();

        private static StorageSystem instance;
        private Logic.Inventory.Storage storage;

        /// <summary>창고 (하나뿐). 접근 지점 · 창고 화면이 찾는다.</summary>
        public static StorageSystem Instance => instance;

        /// <summary>종류별 묶음 (들어온 순서).</summary>
        public IReadOnlyList<ItemStack> Entries => storage != null ? storage.Entries : Array.Empty<ItemStack>();

        /// <summary>창고 내용이 바뀔 때만 발생.</summary>
        public event Action Changed;

        private void Awake()
        {
            if (database == null || startSettings == null)
            {
                Debug.LogError("[StorageSystem] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            if (instance != null && instance != this)
            {
                Debug.LogError("[StorageSystem] 창고가 두 개 있음 — Core 씬에 하나만 둔다", this);
                enabled = false;
                return;
            }
            instance = this;

            storage = new Logic.Inventory.Storage(CanStore);
            foreach (var entry in startSettings.StartingStorage)
            {
                if (entry.Item == null)
                    continue;
                if (!storage.TryDeposit(entry.Item.Id, entry.Count))
                    Debug.LogWarning($"[StorageSystem] 시작 창고의 {entry.Item.Id}를 넣지 못함 (보관 불가 또는 데이터베이스에 없음)", this);
            }
            RefreshDebugView();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public ItemData ItemOf(ItemStack stack) => stack.IsEmpty ? null : database.Get<ItemData>(stack.ItemId);
        public int CountOf(string itemId) => storage != null ? storage.CountOf(itemId) : 0;

        /// <summary>창고에 넣을 수 있는 아이템인지 (데이터베이스에 있고 CanStore).</summary>
        public bool CanStore(string itemId) => database.TryGet(itemId, out ItemData item) && item.CanStore;

        /// <summary>인벤토리 칸 하나를 통째로 넣는다. 빈 칸 · 보관 불가면 false.</summary>
        public bool DepositSlot(PlayerInventory inventory, SlotInventory slots, int index)
        {
            if (storage == null || !StorageRules.DepositSlot(storage, slots, index))
                return false;
            Notify(inventory);
            return true;
        }

        /// <summary>도구를 뺀 보관 가능 아이템을 핫바 · 가방에서 모두 넣는다. 넣은 칸 수.</summary>
        public int DepositAll(PlayerInventory inventory)
        {
            if (storage == null)
                return 0;
            int moved = StorageRules.DepositAll(storage, inventory.Containers, id => !(database.Get<ItemData>(id) is ToolData));
            if (moved > 0)
                Notify(inventory);
            return moved;
        }

        /// <summary>한 종류를 한 묶음만큼(자리가 모자라면 들어가는 만큼) 꺼낸다. 꺼낸 개수 (0 = 자리 없음).</summary>
        public int WithdrawStack(PlayerInventory inventory, string itemId)
        {
            if (storage == null)
                return 0;
            int taken = StorageRules.WithdrawStack(storage, inventory.Containers, itemId);
            if (taken > 0)
                Notify(inventory);
            return taken;
        }

        /// <summary>창고에 있는 fromId 하나를 toItem 하나로 바꾼다 (상점 도구 등급 교체 3-3). 없으면 false.</summary>
        public bool TryReplaceOne(string fromId, ItemData toItem)
        {
            if (storage == null || toItem == null || storage.CountOf(fromId) <= 0 || !storage.TryWithdraw(fromId, 1))
                return false;
            if (!storage.TryDeposit(toItem.Id, 1))
            {
                storage.TryDeposit(fromId, 1); // 되돌림 — 보관 불가 아이템
                return false;
            }
            RefreshDebugView();
            Changed?.Invoke();
            return true;
        }

        private void Notify(PlayerInventory inventory)
        {
            RefreshDebugView();
            Changed?.Invoke();
            inventory.NotifyChanged();
        }

        private void RefreshDebugView()
        {
            contentsView.Clear();
            foreach (var e in storage.Entries)
                contentsView.Add($"{e.ItemId} ×{e.Count}");
        }
    }
}
