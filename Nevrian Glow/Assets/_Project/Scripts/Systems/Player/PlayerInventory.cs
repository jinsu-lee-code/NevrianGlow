using System;
using System.Collections.Generic;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Inventory;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Player
{
    /// <summary>
    /// 통합 인벤토리 = 핫바 + 가방 (GDD 4-3). 칸 내용 · 쌓기 · 넣는 순서(같은 묶음 → 빈 핫바 → 빈 가방)는 Logic 규칙.
    /// 칸에는 아이템 id만 들어가고, 데이터는 ContentDatabase로 찾는다 (저장 6-1도 id).
    /// 가방 화면의 "손에 든 묶음"(Cursor)도 여기서 가진다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerInventory : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("핫바 · 가방 칸 수 (Data/Stats/InventorySettings)")]
        [SerializeField] private InventorySettingsData settings;
        [Tooltip("시작 핫바 (Data/Stats/GameStartSettings)")]
        [SerializeField] private GameStartSettingsData startSettings;
        [Tooltip("아이템 id → 아이템 데이터 (Data/ContentDatabase)")]
        [SerializeField] private ContentDatabase database;

        [Header("디버그")]
        [Tooltip("핫바 내용 (읽기 전용)")]
        [SerializeField] private List<string> hotbarView = new List<string>();
        [Tooltip("가방 내용 (읽기 전용)")]
        [SerializeField] private List<string> bagView = new List<string>();

        private static PlayerInventory instance;
        private SlotInventory hotbar;
        private SlotInventory bag;
        private SlotInventory[] containers;
        private ItemStack cursor;

        /// <summary>플레이어 인벤토리 (플레이어는 하나). 구역 씬의 대상(밭 · 바닥 아이템 · 창고)이 찾는다.</summary>
        public static PlayerInventory Instance => instance;

        public SlotInventory Hotbar => hotbar;
        public SlotInventory Bag => bag;
        /// <summary>넣는 순서대로 (핫바 → 가방).</summary>
        public IReadOnlyList<SlotInventory> Containers => containers;
        public ItemStack Cursor => cursor;
        public InventorySettingsData Settings => settings;

        /// <summary>칸 수 · 내용 · 손에 든 묶음이 바뀔 때만 발생.</summary>
        public event Action Changed;

        private void Awake()
        {
            if (settings == null || startSettings == null || database == null)
            {
                Debug.LogError("[PlayerInventory] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            if (instance != null && instance != this)
            {
                Debug.LogError("[PlayerInventory] 플레이어 인벤토리가 두 개 있음", this);
                enabled = false;
                return;
            }
            instance = this;

            hotbar = new SlotInventory(settings.HotbarBaseSlots, MaxStackOf);
            bag = new SlotInventory(settings.BagBaseSlots, MaxStackOf);
            containers = new[] { hotbar, bag };

            var start = startSettings.StartingHotbar;
            for (int i = 0; i < start.Count; i++)
            {
                var entry = start[i];
                if (entry.Item == null || !IsKnown(entry.Item))
                    continue;
                if (i >= hotbar.SlotCount)
                {
                    Debug.LogWarning($"[PlayerInventory] 시작 핫바 {i + 1}번째 줄({entry.Item.Id})이 칸 수 {hotbar.SlotCount}를 넘어 넣지 못함 — GameStartSettings 확인", this);
                    continue;
                }
                hotbar.TryPlace(i, entry.Item.Id, Mathf.Min(entry.Count, entry.Item.MaxStack));
            }
            RefreshDebugView();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        // ── 조회 ──

        public ItemData ItemOf(ItemStack stack) => stack.IsEmpty ? null : database.Get<ItemData>(stack.ItemId);
        public ItemData ItemAt(SlotInventory inventory, int index) => ItemOf(inventory[index]);
        public ItemData Lookup(string itemId) => database.Get<ItemData>(itemId);

        // ── 넣기 · 빼기 ──

        /// <summary>count개를 전부 넣을 자리가 있는지 (같은 묶음 → 빈 핫바 → 빈 가방).</summary>
        public bool CanAdd(ItemData item, int count) => item != null && hotbar != null && InventoryRules.CanAddAll(containers, item.Id, count);

        /// <summary>count개를 전부 넣는다. 자리가 모자라면 아무것도 넣지 않고 false (수확 · 줍기 실패).</summary>
        public bool TryAdd(ItemData item, int count)
        {
            if (item == null || hotbar == null || !IsKnown(item))
                return false;
            if (!InventoryRules.TryAddAll(containers, item.Id, count))
                return false;
            NotifyChanged();
            return true;
        }

        /// <summary>칸 하나에서 count개를 뺀다. 모자라면 false.</summary>
        public bool TryRemove(SlotInventory inventory, int index, int count)
        {
            if (inventory == null || !inventory.TryRemoveAt(index, count))
                return false;
            NotifyChanged();
            return true;
        }

        // ── 가방 화면 ──

        /// <summary>칸 클릭 (집기 · 놓기 · 바꾸기 · 합치기).</summary>
        public bool ClickSlot(SlotInventory inventory, int index)
        {
            if (!CursorRules.Click(inventory, index, ref cursor))
                return false;
            NotifyChanged();
            return true;
        }

        /// <summary>손에 든 묶음을 인벤토리로 돌려놓는다. 자리가 없으면 false (손에 그대로).</summary>
        public bool ReturnCursor()
        {
            if (cursor.IsEmpty)
                return true;
            if (!CursorRules.ReturnCursor(containers, ref cursor))
                return false;
            NotifyChanged();
            return true;
        }

        // ── 칸 수 ──

        /// <summary>핫바 칸 수 (스킬 핫바 확장 4-3 · 디버그). 1 ~ 최대. 줄이면 없어진 칸의 아이템은 사라진다.</summary>
        public void SetHotbarSlotCount(int count)
        {
            count = Mathf.Clamp(count, 1, settings.HotbarMaxSlots);
            if (hotbar == null || count == hotbar.SlotCount)
                return;
            hotbar.Resize(count);
            NotifyChanged();
        }

        /// <summary>가방 칸 수 (상점 가방 확장 3-3 · 디버그). 0 ~ 최대. 줄이면 없어진 칸의 아이템은 사라진다.</summary>
        public void SetBagSlotCount(int count)
        {
            count = Mathf.Clamp(count, 0, settings.BagMaxSlots);
            if (bag == null || count == bag.SlotCount)
                return;
            bag.Resize(count);
            NotifyChanged();
        }

        /// <summary>창고 등 다른 시스템이 칸을 직접 바꾼 뒤 알린다.</summary>
        public void NotifyChanged()
        {
            RefreshDebugView();
            Changed?.Invoke();
        }

        // ── 내부 ──

        // 칸에는 id만 들어간다 → 다시 찾을 수 없는 아이템(데이터베이스에 없음)은 넣지 않는다.
        private bool IsKnown(ItemData item)
        {
            if (database.TryGet(item.Id, out ItemData _))
                return true;
            Debug.LogError($"[PlayerInventory] 아이템 '{item.Id}'가 ContentDatabase에 없어 넣지 않음 — Glow Farm/Data/Refresh Content Database", this);
            return false;
        }

        public int MaxStackOf(string itemId) => database.TryGet(itemId, out ItemData item) ? item.MaxStack : 1;

        private void RefreshDebugView()
        {
            Fill(hotbarView, hotbar);
            Fill(bagView, bag);
        }

        private static void Fill(List<string> view, SlotInventory inv)
        {
            view.Clear();
            for (int i = 0; i < inv.SlotCount; i++)
            {
                var s = inv[i];
                view.Add(s.IsEmpty ? "-" : $"{s.ItemId} ×{s.Count}");
            }
        }
    }
}
