using System;
using System.Collections.Generic;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace NevrianGlowFarm.Systems.Player
{
    /// <summary>
    /// 핫바 (GDD 4-3). 칸 수는 InventorySettings(기본 4 · 최대 8), 시작 내용은 GameStartSettings의 시작 핫바.
    /// 숫자 키 1~8 · 휠로 고르고, 고른 칸의 아이템이 좌클릭 동작을 정한다.
    /// 수량 쌓기 · 가방 · 내려놓기는 2-2 통합 인벤토리에서 이 칸 구조를 대신한다.
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
        [Tooltip("칸 내용 (읽기 전용, 빈 칸 = None)")]
        [SerializeField] private List<ItemData> slots = new List<ItemData>();

        public int SlotCount => slots.Count;
        public int SelectedIndex => selectedIndex;
        public ItemData SelectedItem => GetItem(selectedIndex);

        /// <summary>선택 칸이 바뀔 때만 발생 (새 칸 번호).</summary>
        public event Action<int> SelectionChanged;

        /// <summary>칸 수나 칸 내용이 바뀔 때만 발생.</summary>
        public event Action SlotsChanged;

        private void Awake()
        {
            if (inventorySettings == null || startSettings == null)
            {
                Debug.LogError("[Hotbar] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }

            slots.Clear();
            int count = inventorySettings.HotbarBaseSlots;
            var start = startSettings.StartingHotbar;
            for (int i = 0; i < count; i++)
                slots.Add(i < start.Count ? start[i] : null);
            if (start.Count > count)
                Debug.LogWarning($"[Hotbar] 시작 핫바 {start.Count}개가 칸 수 {count}보다 많아 뒤의 {start.Count - count}개를 넣지 못함 — GameStartSettings 확인", this);

            selectedIndex = 0;
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

        public ItemData GetItem(int index) => index >= 0 && index < slots.Count ? slots[index] : null;

        /// <summary>칸을 고른다. 없는 칸 · 같은 칸이면 무시.</summary>
        public void Select(int index)
        {
            if (HotbarSelection.TrySelect(index, slots.Count, ref selectedIndex))
                SelectionChanged?.Invoke(selectedIndex);
        }

        /// <summary>한 칸 옮긴다 (양수 = 다음 칸). 끝에서는 반대쪽으로 돈다.</summary>
        public void Cycle(int step)
        {
            if (HotbarSelection.TryCycle(step, slots.Count, ref selectedIndex))
                SelectionChanged?.Invoke(selectedIndex);
        }

        /// <summary>
        /// 칸 수를 바꾼다 (스킬 핫바 확장 4-3 · 디버그 창). 1 ~ 최대 칸 수로 제한.
        /// 줄이면 없어진 칸의 아이템은 사라진다 — 게임에서는 늘리기만 한다.
        /// </summary>
        public void SetSlotCount(int count)
        {
            count = Mathf.Clamp(count, 1, inventorySettings.HotbarMaxSlots);
            if (count == slots.Count)
                return;

            while (slots.Count < count)
                slots.Add(null);
            if (slots.Count > count)
                slots.RemoveRange(count, slots.Count - count);

            SlotsChanged?.Invoke();
            if (HotbarSelection.ClampToSlotCount(slots.Count, ref selectedIndex))
                SelectionChanged?.Invoke(selectedIndex);
        }
    }
}
