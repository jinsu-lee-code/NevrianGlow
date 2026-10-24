using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Text;
using NevrianGlowFarm.Systems.Skill;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 스킬 트리 화면 (K, 어디서나 — GDD 4-1 스킬 단계 표 · TASKS 4-3). 스킬 데이터를 탭(SkillData.tab · tabOrder) · 순서대로 한 줄씩 자동 배치 (5-4c):
    /// [현재/최대] 이름 / 둘째 줄 다음 단계 효과 · 잠김 조건 … 비용 [SP]. 마우스를 올리면 단계별 효과 툴팁. 줄을 누르면 다음 단계를 배운다. K · Esc로 닫는다.
    /// 탭 UI가 없으면(참조 비움) 예전처럼 한 목록에 [탭] 글자를 붙인다.
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
        [Tooltip("탭 버튼 원본 — 비활성 자식으로 두면 탭 수만큼 복제한다 (비우면 탭 없이 한 목록, 5-4c)")]
        [SerializeField] private HotbarSlotView tabTemplate;
        [Tooltip("탭 버튼을 늘어놓을 부모")]
        [SerializeField] private Transform tabParent;
        [Tooltip("비용 옆 SP 아이콘 (비우면 숫자만, 5-4c)")]
        [SerializeField] private Sprite costIcon;
        [Tooltip("보유 SP 글자 — 제목 오른쪽에 따로 (비우면 제목에 같이, 5-4c)")]
        [SerializeField] private TMP_Text pointsLabel;

        [Header("조정값")]
        [Tooltip("배울 수 있는 줄 색")]
        [SerializeField] private Color availableColor = new Color(0.05f, 0.25f, 0.3f, 0.9f);
        [Tooltip("배울 수 없는 줄 색 (선행 · SP 부족)")]
        [SerializeField] private Color unavailableColor = new Color(0.12f, 0.12f, 0.16f, 0.85f);
        [Tooltip("다 배운 줄 색")]
        [SerializeField] private Color maxedColor = new Color(0.25f, 0.2f, 0.4f, 0.85f);
        [Tooltip("고른 탭 색")]
        [SerializeField] private Color tabSelectedColor = new Color(0.15f, 0.45f, 0.5f, 0.95f);
        [Tooltip("고르지 않은 탭 색")]
        [SerializeField] private Color tabNormalColor = new Color(0.08f, 0.14f, 0.18f, 0.9f);

        private readonly List<HotbarSlotView> entries = new List<HotbarSlotView>();
        private readonly List<SkillData> rowSkills = new List<SkillData>();
        private readonly List<HotbarSlotView> tabViews = new List<HotbarSlotView>();
        private readonly List<string> tabNames = new List<string>();
        private int tabIndex;

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
            if (tabTemplate != null)
                tabTemplate.gameObject.SetActive(false);
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
                {
                    // 튜토리얼(5-7): 안내한 스킬을 배우기 전에는 닫히지 않는다
                    if (Systems.Tutorial.TutorialSystem.IsLocked(Logic.Tutorial.TutorialLocks.CloseSkill))
                    {
                        messageLabel.text = "먼저 안내한 스킬을 배워요";
                        AudioManager.Play(SoundCues.UiBuyFail);
                    }
                    else
                        Close();
                }
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
            BuildTabs();
            Rebuild();
        }

        public int TabCount => tabViews.Count;

        /// <summary>탭을 고른다 (탭 클릭 · 디버그).</summary>
        public void SelectTab(int index)
        {
            if (index < 0 || index >= tabViews.Count)
                return;
            tabIndex = index;
            Rebuild();
        }

        // 탭 = 스킬 데이터의 탭 이름을 tabOrder 순으로 (스킬 목록이 바뀌었을 때만 다시 만든다). 탭 UI가 없으면 한 목록
        private void BuildTabs()
        {
            if (tabTemplate == null || tabParent == null)
                return;
            var names = new List<string>();
            var orders = new List<int>();
            foreach (var s in skills.Skills)
            {
                string tab = s.Tab ?? string.Empty;
                int at = names.IndexOf(tab);
                if (at < 0)
                {
                    names.Add(tab);
                    orders.Add(s.TabOrder);
                }
                else if (s.TabOrder < orders[at])
                    orders[at] = s.TabOrder;
            }
            var sorted = new List<int>();
            for (int i = 0; i < names.Count; i++)
                sorted.Add(i);
            sorted.Sort((a, b) => orders[a] != orders[b] ? orders[a].CompareTo(orders[b]) : a.CompareTo(b));
            var ordered = new List<string>(names.Count);
            foreach (int i in sorted)
                ordered.Add(names[i]);

            bool same = ordered.Count == tabNames.Count;
            for (int i = 0; same && i < ordered.Count; i++)
                same = ordered[i] == tabNames[i];
            if (same)
                return;

            foreach (var v in tabViews)
                Destroy(v.gameObject);
            tabViews.Clear();
            tabNames.Clear();
            tabNames.AddRange(ordered);
            for (int i = 0; i < tabNames.Count; i++)
            {
                int index = i;
                var view = Instantiate(tabTemplate, tabParent);
                view.name = $"Tab{i + 1}";
                view.gameObject.SetActive(true);
                view.SetContent(0, string.IsNullOrEmpty(tabNames[i]) ? "기타" : tabNames[i]);
                view.Clicked += () => SelectTab(index);
                tabViews.Add(view);
            }
            tabIndex = Mathf.Clamp(tabIndex, 0, Mathf.Max(0, tabViews.Count - 1));
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
            titleLabel.text = game == null || pointsLabel != null ? "스킬" : $"스킬 - 보유 SP {game.SkillPoints}";
            if (pointsLabel != null)
                pointsLabel.text = game != null ? game.SkillPoints.ToString() : string.Empty;

            bool useTabs = tabViews.Count > 0;
            for (int i = 0; i < tabViews.Count; i++)
                tabViews[i].SetSelected(i == tabIndex, tabNormalColor, tabSelectedColor);

            rowSkills.Clear();
            foreach (var s in skills.Skills)
                if (!useTabs || (s.Tab ?? string.Empty) == tabNames[tabIndex])
                    rowSkills.Add(s);

            while (entries.Count < rowSkills.Count)
            {
                int row = entries.Count;
                var view = Instantiate(entryTemplate, entryParent);
                view.name = $"Skill{row + 1}";
                view.Clicked += () => { if (row < rowSkills.Count) OnEntryClicked(rowSkills[row]); };
                entries.Add(view);
            }
            for (int i = 0; i < entries.Count; i++)
            {
                bool used = i < rowSkills.Count;
                entries[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                var skill = rowSkills[i];
                var check = skills.Check(skill);
                if (check == LearnCheck.Ok && skills.IsTutorialLocked(skill))
                    check = LearnCheck.Prerequisite; // 튜토리얼이 잠근 스킬은 못 배우는 줄로 (회색)
                int level = skills.LevelOf(skill);
                var view = entries[i];
                view.SetIcon(null);
                string head = useTabs || string.IsNullOrEmpty(skill.Tab) ? string.Empty : $"[{skill.Tab}] ";
                view.SetContent(0, head + skill.DisplayName);
                view.SetKeyText(UiText.SkillLevel(level, skill.MaxLevel));
                view.SetDetail(DetailText(skill, level, check));
                view.SetValue(check == LearnCheck.MaxLevel ? string.Empty : skills.NextCost(skill).ToString(), costIcon);
                view.BindTooltip(() => TooltipText.ForSkill(skill, skills.LevelOf(skill), skills.Check(skill), skills.NextCost(skill)));
                var color = check == LearnCheck.Ok ? availableColor : check == LearnCheck.MaxLevel ? maxedColor : unavailableColor;
                view.SetSelected(false, color, color);
                view.SetDimmed(check != LearnCheck.Ok && check != LearnCheck.MaxLevel);
            }
        }

        // 둘째 줄: 다음 단계 효과 + 못 배우는 이유. 게임 화면 글자에는 "—"를 쓰지 않는다 (글꼴에 없음, TASKS 트러블슈팅)
        private static string DetailText(SkillData skill, int level, LearnCheck check)
        {
            if (check == LearnCheck.MaxLevel)
                return UiText.Join(TooltipText.EffectText(skill, level), "다 배움");
            string next = $"다음: {TooltipText.EffectText(skill, level + 1)}";
            switch (check)
            {
                case LearnCheck.Prerequisite:
                    return UiText.Join(next, skill.Prerequisite != null ? $"잠김 ({skill.Prerequisite.DisplayName} {skill.PrerequisiteLevel}단계 필요)" : "잠김");
                case LearnCheck.NotEnoughPoints: return UiText.Join(next, "SP 부족");
                default: return next;
            }
        }
    }
}
