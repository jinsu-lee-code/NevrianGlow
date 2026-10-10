using System;
using System.Collections.Generic;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Inventory;
using NevrianGlowFarm.Logic.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace NevrianGlowFarm.Systems.Player
{
    /// <summary>
    /// 핫바 (GDD 4-3). 칸 수는 InventorySettings(기본 4 · 최대 8), 시작 내용은 GameStartSettings의 시작 핫바.
    /// 숫자 키 1~8 · 휠로 고르고, 고른 칸의 아이템이 좌클릭 동작을 정한다.
    /// 칸 내용 · 쌓기 규칙은 Logic의 SlotInventory · InventoryRules — 2-2에서 가방 · 창고가 같은 규칙으로 이어 붙는다.
    /// </summary>
    public class Hotbar : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("핫바 칸 수 (Data/Stats/InventorySettings)")]
        [SerializeField] private InventorySettingsData inventorySettings;
        [Tooltip("시작 핫바 내용 (Data/Stats/GameStartSettings)")]
        [SerializeField] private GameStartSettingsData startSettings;
        [Tooltip("핫바 숫자 키 입력 (Player/Hotbar, 키 1~8)")]
        [SerializeField] private InputActionReference slotAction;
        [Tooltip("핫바 휠 입력 (Player/HotbarScroll, 마우스 휠)")]
        [SerializeField] private InputActionReference scrollAction;

        [Header("조정값")]
        [Tooltip("켜면 휠을 아래로 굴릴 때 다음 칸 (꺼면 위로 굴릴 때 다음 칸)")]
        [SerializeField] private bool scrollDownIsNext = true;

        [Header("디버그")]
        [Tooltip("선택된 칸 번호 (0부터, 읽기 전용)")]
        [SerializeField] private int selectedIndex;
        [Tooltip("칸 내용 (읽기 전용)")]
        [SerializeField] private List<string> slotView = new List<string>();

        private SlotInventory slots;
        private SlotInventory[] containers;
        private readonly Dictionary<string, ItemData> items = new Dictionary<string, ItemData>();

        public int SlotCount => slots != null ? slots.SlotCount : 0;
        public int SelectedIndex => selectedIndex;
        public ItemData SelectedItem => GetItem(selectedIndex);
        public int SelectedCount => GetCount(selectedIndex);

        /// <summary>선택 칸이 바뀔 때만 발생 (새 칸 번호).</summary>
        public event Action<int> SelectionChanged;

        /// <summary>칸 수나 칸 내용(종류 · 개수)이 바뀔 때만 발생.</summary>
        public event Action SlotsChanged;

        private void Awake()
        {
            if (inventorySettings == null || startSettings == null)
            {
                Debug.LogError("[Hotbar] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }

            slots = new SlotInventory(inventorySettings.HotbarBaseSlots, MaxStackOf);
            containers = new[] { slots };

            var start = startSettings.StartingHotbar;
            for (int i = 0; i < start.Count; i++)
            {
                var entry = start[i];
                if (entry.Item == null)
                    continue;
                if (i >= slots.SlotCount)
                {
                    Debug.LogWarning($"[Hotbar] 시작 핫바 {i + 1}번째 줄({entry.Item.Id})이 칸 수 {slots.SlotCount}를 넘어 넣지 못함 — GameStartSettings 확인", this);
                    continue;
                }
                Register(entry.Item);
                slots.TryPlace(i, entry.Item.Id, Mathf.Min(entry.Count, entry.Item.MaxStack));
            }

            selectedIndex = 0;
            RefreshDebugView();
        }

        private void OnEnable()
        {
            if (slotAction != null) slotAction.action.Enable();
            if (scrollAction != null) scrollAction.action.Enable();
        }

        private void Update()
        {
            if (slotAction != null && slotAction.action.WasPressedThisFrame()
                && slotAction.action.activeControl is KeyControl key && key.keyCode >= Key.Digit1 && key.keyCode <= Key.Digit9)
                Select(key.keyCode - Key.Digit1);

            if (scrollAction != null)
            {
                float scroll = scrollAction.action.ReadValue<float>();
                if (scroll != 0f)
                    Cycle(scrollDownIsNext ? -Math.Sign(scroll) : Math.Sign(scroll));
            }
        }

        public ItemData GetItem(int index)
        {
            if (slots == null)
                return null;
            var stack = slots[index];
            return !stack.IsEmpty && items.TryGetValue(stack.ItemId, out var item) ? item : null;
        }

        public int GetCount(int index) => slots != null ? slots[index].Count : 0;

        /// <summary>칸을 고른다. 없는 칸 · 같은 칸이면 무시.</summary>
        public void Select(int index)
        {
            if (HotbarSelection.TrySelect(index, SlotCount, ref selectedIndex))
                SelectionChanged?.Invoke(selectedIndex);
        }

        /// <summary>한 칸 옮긴다 (양수 = 다음 칸). 끝에서는 반대쪽으로 돈다.</summary>
        public void Cycle(int step)
        {
            if (HotbarSelection.TryCycle(step, SlotCount, ref selectedIndex))
                SelectionChanged?.Invoke(selectedIndex);
        }

        /// <summary>count개를 전부 넣을 자리가 있는지 (같은 아이템 칸 → 빈 칸).</summary>
        public bool CanAdd(ItemData item, int count) => slots != null && item != null && InventoryRules.CanAddAll(containers, item.Id, count);

        /// <summary>count개를 전부 넣는다. 자리가 모자라면 아무것도 넣지 않고 false (수확 · 줍기 실패).</summary>
        public bool TryAdd(ItemData item, int count)
        {
            if (slots == null || item == null)
                return false;
            Register(item);
            if (!InventoryRules.TryAddAll(containers, item.Id, count))
                return false;
            NotifySlotsChanged();
            return true;
        }

        /// <summary>고른 칸에서 count개를 쓴다 (씨앗 심기 · 비료). 모자라면 false.</summary>
        public bool TryConsumeSelected(int count)
        {
            if (slots == null || !slots.TryRemoveAt(selectedIndex, count))
                return false;
            NotifySlotsChanged();
            return true;
        }

        /// <summary>
        /// 칸 수를 바꾼다 (스킬 핫바 확장 4-3 · 디버그 창). 1 ~ 최대 칸 수로 제한.
        /// 줄이면 없어진 칸의 아이템은 사라진다 — 게임에서는 늘리기만 한다.
        /// </summary>
        public void SetSlotCount(int count)
        {
            if (slots == null)
                return;
            count = Mathf.Clamp(count, 1, inventorySettings.HotbarMaxSlots);
            if (count == slots.SlotCount)
                return;

            slots.Resize(count);
            NotifySlotsChanged();
            if (HotbarSelection.ClampToSlotCount(slots.SlotCount, ref selectedIndex))
                SelectionChanged?.Invoke(selectedIndex);
        }

        // 아이템 에셋을 id로 다시 찾기 위한 표. 전체 목록은 2-1 ContentDatabase가 대신한다.
        private void Register(ItemData item)
        {
            if (item != null && !string.IsNullOrEmpty(item.Id))
                items[item.Id] = item;
        }

        private int MaxStackOf(string itemId) => items.TryGetValue(itemId, out var item) ? item.MaxStack : 1;

        private void NotifySlotsChanged()
        {
            RefreshDebugView();
            SlotsChanged?.Invoke();
        }

        private void RefreshDebugView()
        {
            slotView.Clear();
            for (int i = 0; i < slots.SlotCount; i++)
            {
                var s = slots[i];
                slotView.Add(s.IsEmpty ? "-" : $"{s.ItemId} ×{s.Count}");
            }
        }
    }
}
