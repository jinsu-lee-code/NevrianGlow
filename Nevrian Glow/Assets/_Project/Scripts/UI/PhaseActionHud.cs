using System;
using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Systems.Zones;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 단계 넘기기 HUD 버튼 (TASKS P-7, 회신2 4절 #1 · 결정 2026-10-21): 그 단계에서만 보이는 버튼 + 단축키 표시.
    /// 1인칭이라 평소 커서가 잠겨 있어 단축키로 누른다 (버튼 클릭도 같은 일 — 커서가 보일 때).
    /// 농사: "농사 끝내기 [Enter]" = 농장 문과 같은 길(페이드 → 가게, 확인 없이). 장사: "장사 건너뛰기 [Space]" = SalesSystem이 키를 받는다(여기는 표시 · 클릭만).
    /// </summary>
    public class PhaseActionHud : MonoBehaviour
    {
        /// <summary>버튼이 하는 일. 끝에만 추가.</summary>
        public enum ActionKind
        {
            /// <summary>그 단계를 끝내는 문을 쓴다 (ZoneDoor.TryUseFor).</summary>
            UseDoor,
            /// <summary>장사 건너뛰기 (SalesSystem.FinishNow).</summary>
            SkipSales,
        }

        [Serializable]
        public class Entry
        {
            [Tooltip("이 단계에서만 버튼이 보인다")]
            [SerializeField] private DayPhase phase;
            [Tooltip("버튼 글자 (게임 화면 — '—' 쓰지 않음)")]
            [SerializeField] private string label;
            [Tooltip("버튼에 적을 키 이름 (예: Enter)")]
            [SerializeField] private string keyHint;
            [Tooltip("누르면 하는 일")]
            [SerializeField] private ActionKind action;
            [Tooltip("단축키 — 비우면 키는 다른 곳이 받는다 (예: 장사 건너뛰기 Space는 SalesSystem)")]
            [SerializeField] private InputActionReference key;

            public DayPhase Phase => phase;
            public string Label => label;
            public string KeyHint => keyHint;
            public ActionKind Action => action;
            public InputActionReference Key => key;
        }

        [Header("참조")]
        [Tooltip("버튼 (배경 포함 — 해당 단계가 아니면 숨김)")]
        [SerializeField] private Button button;
        [Tooltip("버튼 글자")]
        [SerializeField] private TMP_Text label;

        [Header("조정값")]
        [Tooltip("단계별 버튼")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Entry current;

        private void Awake()
        {
            if (button == null || label == null)
            {
                Debug.LogError("[PhaseActionHud] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            button.onClick.AddListener(Run);
        }

        private void OnEnable()
        {
            foreach (var e in entries)
                if (e.Key != null)
                    e.Key.action.Enable();
            GameEvents.PhaseChanged += OnPhaseChanged;
            var game = GameManager.Instance;
            Show(game != null && game.IsStarted ? game.Phase : (DayPhase?)null);
        }

        private void OnDisable() => GameEvents.PhaseChanged -= OnPhaseChanged;

        private void OnPhaseChanged(PhaseChange change) => Show(change.Phase);

        private void Show(DayPhase? phase)
        {
            current = null;
            if (phase.HasValue)
                foreach (var e in entries)
                    if (e.Phase == phase.Value)
                    {
                        current = e;
                        break;
                    }
            button.gameObject.SetActive(current != null);
            if (current != null)
                label.text = string.IsNullOrEmpty(current.KeyHint) ? current.Label : $"{current.Label} [{current.KeyHint}]";
        }

        private void Update()
        {
            // 화면이 열려 있거나 이동 중이면 키를 받지 않는다 (Enter가 화면 안 입력과 겹치지 않게)
            if (current == null || current.Key == null || PlayerInputGate.IsBlocked || GameScreen.Current != null)
                return;
            if (current.Key.action.WasPressedThisFrame())
                Run();
        }

        private void Run()
        {
            if (current == null)
                return;
            switch (current.Action)
            {
                case ActionKind.UseDoor:
                    ZoneDoor.TryUseFor(current.Phase);
                    break;
                case ActionKind.SkipSales:
                    var sales = Systems.Sales.SalesSystem.Instance;
                    if (sales != null && sales.IsRunning)
                        sales.FinishNow(advancePhase: true);
                    break;
            }
        }
    }
}
