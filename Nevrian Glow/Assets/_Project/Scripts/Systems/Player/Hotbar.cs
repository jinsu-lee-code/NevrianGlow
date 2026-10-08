using System;
using System.Collections.Generic;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace NevrianGlowFarm.Systems.Player
{
    /// <summary>숫자 키로 도구를 고른다 (BALANCE 9-4). 칸 수 = 도구 목록 길이.</summary>
    public class Hotbar : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("핫바 순서대로 놓을 도구 — 1번 키가 첫 칸 (Data/Tools)")]
        [SerializeField] private List<ToolData> tools = new List<ToolData>();
        [Tooltip("핫바 숫자 키 입력 (Player/Hotbar, 키 1~9)")]
        [SerializeField] private InputActionReference slotAction;

        [Header("디버그")]
        [Tooltip("선택된 칸 번호 (0부터, 읽기 전용)")]
        [SerializeField] private int selectedIndex;

        public IReadOnlyList<ToolData> Tools => tools;
        public int SelectedIndex => selectedIndex;
        public ToolData SelectedTool => selectedIndex >= 0 && selectedIndex < tools.Count ? tools[selectedIndex] : null;

        /// <summary>선택 칸이 바뀔 때만 발생 (새 칸 번호).</summary>
        public event Action<int> SelectionChanged;

        private void OnEnable()
        {
            if (slotAction != null)
                slotAction.action.Enable();
        }

        private void Update()
        {
            if (slotAction == null || !slotAction.action.WasPressedThisFrame())
                return;

            if (slotAction.action.activeControl is KeyControl key && key.keyCode >= Key.Digit1 && key.keyCode <= Key.Digit9)
                Select(key.keyCode - Key.Digit1);
        }

        /// <summary>칸을 고른다. 없는 칸 · 같은 칸이면 무시.</summary>
        public void Select(int index)
        {
            if (HotbarSelection.TrySelect(index, tools.Count, ref selectedIndex))
                SelectionChanged?.Invoke(selectedIndex);
        }
    }
}
