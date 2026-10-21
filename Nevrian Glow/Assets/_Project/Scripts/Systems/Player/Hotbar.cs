using System;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace NevrianGlowFarm.Systems.Player
{
    /// <summary>
    /// 핫바 선택 (GDD 4-3). 칸 내용은 PlayerInventory의 핫바, 여기는 숫자 키 1~8 · 휠로 고르는 일만 한다.
    /// 고른 칸의 아이템이 좌클릭 동작을 정한다. 조작이 잠겨 있으면(가방 · 창고 화면) 입력을 받지 않는다.
    /// </summary>
    public class Hotbar : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("칸 내용을 가진 통합 인벤토리 (같은 Player)")]
        [SerializeField] private PlayerInventory inventory;
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

        public PlayerInventory Inventory => inventory;
        public int SlotCount => inventory != null && inventory.Hotbar != null ? inventory.Hotbar.SlotCount : 0;
        public int SelectedIndex => selectedIndex;
        public ItemData SelectedItem => GetItem(selectedIndex);
        public int SelectedCount => GetCount(selectedIndex);

        /// <summary>선택 칸이 바뀔 때만 발생 (새 칸 번호).</summary>
        public event Action<int> SelectionChanged;

        /// <summary>인벤토리(핫바 · 가방) 칸 수나 내용이 바뀔 때만 발생.</summary>
        public event Action SlotsChanged;

        private void Awake()
        {
            if (inventory == null)
            {
                Debug.LogError("[Hotbar] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (slotAction != null) slotAction.action.Enable();
            if (scrollAction != null) scrollAction.action.Enable();
            if (inventory != null) inventory.Changed += OnInventoryChanged;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
        }

        private void Update()
        {
            if (PlayerInputGate.IsBlocked)
                return;

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

        public ItemData GetItem(int index) => inventory != null && inventory.Hotbar != null ? inventory.ItemAt(inventory.Hotbar, index) : null;
        public int GetCount(int index) => inventory != null && inventory.Hotbar != null ? inventory.Hotbar[index].Count : 0;

        /// <summary>칸을 고른다. 없는 칸 · 같은 칸이면 무시.</summary>
        public void Select(int index)
        {
            if (HotbarSelection.TrySelect(index, SlotCount, ref selectedIndex))
            {
                AudioManager.Play(SoundCues.UiToolSwitch);
                SelectionChanged?.Invoke(selectedIndex);
            }
        }

        /// <summary>한 칸 옮긴다 (양수 = 다음 칸). 끝에서는 반대쪽으로 돈다.</summary>
        public void Cycle(int step)
        {
            if (HotbarSelection.TryCycle(step, SlotCount, ref selectedIndex))
            {
                AudioManager.Play(SoundCues.UiToolSwitch);
                SelectionChanged?.Invoke(selectedIndex);
            }
        }

        /// <summary>인벤토리 전체(핫바 → 가방)에 넣을 자리가 있는지.</summary>
        public bool CanAdd(ItemData item, int count) => inventory != null && inventory.CanAdd(item, count);

        /// <summary>인벤토리 전체(같은 묶음 → 빈 핫바 → 빈 가방)에 넣는다. 자리가 모자라면 false.</summary>
        public bool TryAdd(ItemData item, int count) => inventory != null && inventory.TryAdd(item, count);

        /// <summary>고른 칸에서 count개를 쓴다 (씨앗 심기 · 비료 · 내려놓기). 모자라면 false.</summary>
        public bool TryConsumeSelected(int count) => inventory != null && inventory.TryRemove(inventory.Hotbar, selectedIndex, count);

        /// <summary>핫바 칸 수를 바꾼다 (스킬 4-3 · 디버그). 선택 칸이 없어지면 마지막 칸.</summary>
        public void SetSlotCount(int count)
        {
            if (inventory != null)
                inventory.SetHotbarSlotCount(count);
        }

        private void OnInventoryChanged()
        {
            SlotsChanged?.Invoke();
            if (HotbarSelection.ClampToSlotCount(SlotCount, ref selectedIndex))
                SelectionChanged?.Invoke(selectedIndex);
        }
    }
}
