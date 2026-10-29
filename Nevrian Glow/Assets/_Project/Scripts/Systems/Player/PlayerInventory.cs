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
        [Tooltip("핫바 칸 수 능력치 — 처음 칸 수에 더함, 최대 칸 수까지 (Data/Stats/stat_hotbar_slots, 비우면 그대로)")]
        [SerializeField] private StatData hotbarSlotsStat;

        [Header("디버그")]
        [Tooltip("핫바 내용 (읽기 전용)")]
        [SerializeField] private List<string> hotbarView = new List<string>();
        [Tooltip("가방 내용 (읽기 전용)")]
        [SerializeField] private List<string> bagView = new List<string>();

        private static PlayerInventory instance;
        private SlotInventory hotbar;
        private SlotInventory bag;
        private SlotInventory[] containers;
        private SlotInventory[] bagOnly;
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
            bagOnly = new[] { bag };

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

        private void OnEnable() => Core.GameEvents.StatsChanged += OnStatsChanged;
        private void OnDisable() => Core.GameEvents.StatsChanged -= OnStatsChanged;

        // 핫바 확장 스킬 (4-3): 처음 칸 수 + 능력치. 늘어날 때만 — 줄어드는 스킬은 없다(칸의 아이템을 지우지 않게).
        private void OnStatsChanged()
        {
            if (hotbar == null)
                return;
            int wanted = Mathf.Clamp(Core.StatSystem.Apply(hotbarSlotsStat, settings.HotbarBaseSlots), 1, settings.HotbarMaxSlots);
            if (wanted > hotbar.SlotCount)
                SetHotbarSlotCount(wanted);
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

        /// <summary>
        /// 핫바 칸 하나에서 count개를 쓴다. 그 칸이 비면 가방에 있는 같은 아이템 한 묶음이 그 칸으로 내려온다
        /// (씨앗 · 비료 · 진열 — 2026-10-30 사용자 요청). 모자라면 false.
        /// </summary>
        public bool TryUseFromHotbar(int index, int count)
        {
            if (hotbar == null)
                return false;
            string itemId = hotbar[index].ItemId;
            if (!hotbar.TryRemoveAt(index, count))
                return false;
            InventoryRules.RefillFrom(hotbar, index, itemId, bagOnly);
            NotifyChanged();
            return true;
        }

        /// <summary>핫바 · 가방에 있는 그 아이템을 모두 뺀다 (쓰레기통 비우기). 뺀 개수.</summary>
        public int RemoveAll(ItemData item)
        {
            if (item == null || hotbar == null)
                return 0;
            int removed = InventoryRules.RemoveAll(containers, item.Id);
            if (removed > 0)
                NotifyChanged();
            return removed;
        }

        /// <summary>
        /// 핫바 · 가방(또는 손에 든 묶음)에 있는 fromId 하나를 같은 칸에서 toItem 하나로 바꾼다 (상점 도구 등급 교체 3-3). 없으면 false.
        /// </summary>
        public bool TryReplaceOne(string fromId, ItemData toItem)
        {
            if (fromId == null || toItem == null || hotbar == null || !IsKnown(toItem))
                return false;
            foreach (var slots in containers)
            {
                for (int i = 0; i < slots.SlotCount; i++)
                {
                    if (slots[i].ItemId != fromId || !slots.TryRemoveAt(i, 1))
                        continue;
                    if (!slots.TryPlace(i, toItem.Id, 1))
                    {
                        slots.TryPlace(i, fromId, 1); // 되돌림 — 칸에 다른 묶음이 남은 경우
                        continue;
                    }
                    NotifyChanged();
                    return true;
                }
            }
            if (cursor.ItemId == fromId && cursor.Count == 1)
            {
                cursor = new ItemStack(toItem.Id, 1);
                NotifyChanged();
                return true;
            }
            return false;
        }

        /// <summary>핫바 · 가방에 있는 그 아이템 개수 (손에 든 묶음 제외).</summary>
        public int CountOf(ItemData item) => item == null || hotbar == null ? 0 : InventoryRules.CountOf(containers, item.Id);

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

        /// <summary>핫바 칸 수 (스킬 핫바 확장 4-3 — 능력치가 바뀔 때 · 디버그). 1 ~ 최대. 줄이면 없어진 칸의 아이템은 사라진다.</summary>
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

        // ── 저장 (6-1) ──

        /// <summary>핫바 · 가방을 담는다. 손에 든 묶음은 먼저 돌려놓고(못 돌려놓으면 그 묶음은 저장되지 않음).</summary>
        public void CaptureTo(Logic.Save.SaveSnapshot into)
        {
            if (hotbar == null)
                return;
            if (!cursor.IsEmpty && !ReturnCursor())
                Debug.LogWarning($"[PlayerInventory] 손에 든 {cursor.ItemId} ×{cursor.Count}를 둘 칸이 없어 저장에서 빠짐", this);
            Logic.Save.SaveState.Capture(hotbar, into.Hotbar);
            Logic.Save.SaveState.Capture(bag, into.Bag);
        }

        public void LoadFrom(Logic.Save.SaveSnapshot from)
        {
            if (hotbar == null)
                return;
            cursor = ItemStack.Empty;
            int clipped = hotbar.Load(Clamp(from.Hotbar, settings.HotbarMaxSlots)) + bag.Load(Clamp(from.Bag, settings.BagMaxSlots));
            if (clipped > 0)
                Debug.LogWarning($"[PlayerInventory] 칸당 최대를 넘는 개수 {clipped}개를 잘라 불러옴 (아이템 데이터의 칸당 개수가 줄었음)", this);
            NotifyChanged();
        }

        private static IReadOnlyList<ItemStack> Clamp(List<ItemStack> stacks, int max) =>
            stacks.Count <= max ? stacks : stacks.GetRange(0, max);

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
