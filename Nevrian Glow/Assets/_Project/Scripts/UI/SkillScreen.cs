using System.Collections.Generic;
using System.Globalization;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Systems.Skill;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 스킬 트리 화면 (K, 어디서나 — GDD 4-1 스킬 단계 표 · TASKS 4-3). 스킬 데이터를 탭 · 순서대로 한 줄씩 자동 배치:
    /// [탭] 이름 단계/최대 - 다음 단계 효과 · 비용 · 상태. 줄을 누르면 다음 단계를 배운다. K · Esc로 닫는다.
    /// 모양은 UI를 몰아서 다듬을 때(5-4)까지 상점 화면과 같은 그레이박스.
    /// </summary>
    public class SkillScreen : GameScreen
    {
        [Header("참조")]
        [Tooltip("스킬 배우기 (Core SkillSystem)")]
        [SerializeField] private SkillSystem skills;
        [Tooltip("스킬 줄 원본 — 비활성 자식으로 두면 스킬 수만큼 복제한다")]
        [SerializeField] private HotbarSlotView entryTemplate;
        [Tooltip("스킬 줄을 늘어놓을 부모")]
        [SerializeField] private Transform entryParent;
        [Tooltip("제목 (보유 SP)")]
        [SerializeField] private TMP_Text titleLabel;
        [Tooltip("안내 한 줄")]
        [SerializeField] private TMP_Text messageLabel;
        [Tooltip("열기 · 닫기 입력 (Player/Skill, K)")]
        [SerializeField] private InputActionReference toggleAction;
        [Tooltip("닫기 입력 (UI/Cancel, Esc)")]
        [SerializeField] private InputActionReference cancelAction;

        [Header("조정값")]
        [Tooltip("배울 수 있는 줄 색")]
        [SerializeField] private Color availableColor = new Color(0.05f, 0.25f, 0.3f, 0.9f);
        [Tooltip("배울 수 없는 줄 색 (선행 · SP 부족)")]
        [SerializeField] private Color unavailableColor = new Color(0.12f, 0.12f, 0.16f, 0.85f);
        [Tooltip("다 배운 줄 색")]
        [SerializeField] private Color maxedColor = new Color(0.25f, 0.2f, 0.4f, 0.85f);

        private readonly List<HotbarSlotView> entries = new List<HotbarSlotView>();

        public static SkillScreen Instance { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (skills == null || entryTemplate == null || entryParent == null || titleLabel == null || messageLabel == null || toggleAction == null || cancelAction == null)
            {
                Debug.LogError("[SkillScreen] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            Instance = this;
            entryTemplate.gameObject.SetActive(false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
                Instance = null;
        }

        private void OnEnable()
        {
            toggleAction.action.Enable();
            cancelAction.action.Enable();
            skills.Changed += Rebuild;
        }

        private void OnDisable() => skills.Changed -= Rebuild;

        private void Update()
        {
            if (IsOpen)
            {
                if (toggleAction.action.WasPressedThisFrame() || cancelAction.action.WasPressedThisFrame())
                    Close();
                return;
            }
            // 다른 화면이 열려 있거나 이번 프레임에 닫혔으면 열지 않는다.
            if (Current == null && !ClosedThisFrame && toggleAction.action.WasPressedThisFrame())
                Open();
        }

        public void Open() => Show();

        protected override void OnOpened()
        {
            messageLabel.text = "줄을 누르면 다음 단계를 배운다";
            Rebuild();
        }

        /// <summary>스킬 줄 하나의 다음 단계를 배운다 (화면 클릭 · 디버그).</summary>
        public void OnEntryClicked(SkillData skill)
        {
            if (!IsOpen || skill == null)
                return;
            bool learned = skills.TryLearn(skill, out string message);
            AudioManager.Play(learned ? SoundCues.UiBuySuccess : SoundCues.UiBuyFail);
            messageLabel.text = message;
        }

        private void Rebuild()
        {
            if (!IsOpen)
                return;
            var game = GameManager.Instance;
            titleLabel.text = game == null ? "스킬" : $"스킬 - 보유 SP {game.SkillPoints}";

            var list = skills.Skills;
            while (entries.Count < list.Count)
            {
                int row = entries.Count;
                var view = Instantiate(entryTemplate, entryParent);
                view.name = $"Skill{row + 1}";
                view.Clicked += () => { if (row < skills.Skills.Count) OnEntryClicked(skills.Skills[row]); };
                entries.Add(view);
            }
            for (int i = 0; i < entries.Count; i++)
            {
                bool used = i < list.Count;
                entries[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                var skill = list[i];
                var check = skills.Check(skill);
                entries[i].SetContent(0, RowText(skill, check));
                var color = check == LearnCheck.Ok ? availableColor : check == LearnCheck.MaxLevel ? maxedColor : unavailableColor;
                entries[i].SetSelected(false, color, color);
            }
        }

        // 게임 화면 글자에는 "—"를 쓰지 않는다 (글꼴에 없음, TASKS 트러블슈팅)
        private string RowText(SkillData skill, LearnCheck check)
        {
            string head = string.IsNullOrEmpty(skill.Tab) ? string.Empty : $"[{skill.Tab}] ";
            int level = skills.LevelOf(skill);
            string text = $"{head}{skill.DisplayName} {level}/{skill.MaxLevel}단계";
            if (check == LearnCheck.MaxLevel)
                return $"{text} - {EffectText(skill, level)} · 다 배움";
            string next = $"{text} - 다음: {EffectText(skill, level + 1)} · {skills.NextCost(skill)} SP";
            switch (check)
            {
                case LearnCheck.Prerequisite:
                    return skill.Prerequisite != null ? $"{next} · 잠김 ({skill.Prerequisite.DisplayName} {skill.PrerequisiteLevel}단계 필요)" : $"{next} · 잠김";
                case LearnCheck.NotEnoughPoints: return $"{next} · SP 부족";
                default: return next;
            }
        }

        // 단계 설명이 있으면 그대로, 없으면 능력치 변경 값으로 (예: 판매가 ×1.1)
        private static string EffectText(SkillData skill, int level)
        {
            string described = skill.LevelDescription(level);
            if (described != null)
                return described;
            var parts = new List<string>();
            var mods = new List<StatModifier>();
            skill.CollectModifiers(level, mods);
            foreach (var m in mods)
            {
                string name = m.StatId;
                foreach (var e in skill.Modifiers)
                    if (e.Stat != null && e.Stat.Id == m.StatId)
                        name = e.Stat.DisplayName;
                string value = m.Value.ToString("0.##", CultureInfo.InvariantCulture);
                parts.Add(m.Op == ModifierOp.Add ? $"{name} {(m.Value >= 0 ? "+" : string.Empty)}{value}" : $"{name} ×{value}");
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "효과 없음";
        }
    }
}
