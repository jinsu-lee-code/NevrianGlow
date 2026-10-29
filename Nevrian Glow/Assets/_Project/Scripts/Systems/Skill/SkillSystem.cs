using System;
using System.Collections.Generic;
using System.Linq;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Skill
{
    /// <summary>
    /// 스킬 배우기 (TASKS 4-3 축소판): SP를 쓰고 단계를 올린다. 규칙(최대 단계 · 선행 · SP)은 Logic SkillRules,
    /// 단계 보관은 GameManager(상점 해금 조건이 읽음), 효과 계산은 StatSystem. Core 씬에 하나.
    /// 스킬 목록은 ContentDatabase에서 탭 · 순서대로 — 스킬 추가는 에셋만.
    /// </summary>
    public class SkillSystem : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("스킬 목록 (Data/ContentDatabase)")]
        [SerializeField] private ContentDatabase database;

        private static SkillSystem instance;
        private readonly List<SkillData> skills = new List<SkillData>();

        public static SkillSystem Instance => instance;

        /// <summary>탭 → 순서 → id 순 (스킬 화면 배치 순서).</summary>
        public IReadOnlyList<SkillData> Skills => skills;

        /// <summary>단계 · SP가 바뀌어 화면을 다시 그려야 할 때.</summary>
        public event Action Changed;

        private void Awake()
        {
            if (database == null)
            {
                Debug.LogError("[SkillSystem] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            instance = this;
            skills.AddRange(database.All<SkillData>()
                .Where(s => s != null && !string.IsNullOrEmpty(s.Id) && !s.Hidden) // 숨긴 스킬(기획 phase2 · 연결 전)은 화면에 없다
                .OrderBy(s => s.TabOrder)
                .ThenBy(s => s.Tab ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(s => s.Order)
                .ThenBy(s => s.Id, StringComparer.Ordinal));
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void OnEnable()
        {
            GameEvents.SkillLevelChanged += OnSkillLevelChanged;
            GameEvents.SkillPointsChanged += OnSkillPointsChanged;
        }

        private void OnDisable()
        {
            GameEvents.SkillLevelChanged -= OnSkillLevelChanged;
            GameEvents.SkillPointsChanged -= OnSkillPointsChanged;
        }

        private void OnSkillLevelChanged(string id, int level) => Changed?.Invoke();
        private void OnSkillPointsChanged(int sp) => Changed?.Invoke();

        public int LevelOf(SkillData skill) => skill != null && GameManager.Instance != null ? GameManager.Instance.SkillLevel(skill.Id) : 0;

        public int NextCost(SkillData skill) => skill == null ? 0 : SkillRules.NextCost(skill.ToSpec(), LevelOf(skill));

        public LearnCheck Check(SkillData skill)
        {
            var game = GameManager.Instance;
            if (skill == null || game == null || !game.IsStarted)
                return LearnCheck.MaxLevel;
            return SkillRules.CanLearn(skill.ToSpec(), LevelOf(skill), game.SkillLevel, game.SkillPoints);
        }

        /// <summary>튜토리얼(5-7)이 이 스킬을 잠갔는지 — 안내한 스킬만 배울 수 있는 단계.</summary>
        public bool IsTutorialLocked(SkillData skill) =>
            Systems.Tutorial.TutorialSystem.IsLockedFor(Logic.Tutorial.TutorialLocks.OtherSkills, skill != null ? skill.Id : null);

        /// <summary>다음 단계를 배운다. 결과 문구는 화면 안내용 (게임 화면 글자라 "—"를 쓰지 않는다).</summary>
        public bool TryLearn(SkillData skill, out string message)
        {
            var game = GameManager.Instance;
            var check = Check(skill);
            if (check != LearnCheck.Ok)
            {
                message = FailText(skill, check);
                return false;
            }
            if (IsTutorialLocked(skill))
            {
                message = "지금은 안내한 스킬만 배울 수 있어요";
                return false;
            }
            int cost = NextCost(skill);
            int next = LevelOf(skill) + 1;
            if (!game.TryChangeSkillPoints(-cost))
            {
                message = FailText(skill, LearnCheck.NotEnoughPoints);
                return false;
            }
            game.SetSkillLevel(skill.Id, next);
            message = $"{skill.DisplayName} {next}단계를 배웠다 (SP -{cost})";
            Debug.Log($"[SkillSystem] {skill.Id} {next}단계 (SP -{cost}, 남은 SP {game.SkillPoints})");
            return true;
        }

        public string FailText(SkillData skill, LearnCheck check)
        {
            switch (check)
            {
                case LearnCheck.MaxLevel: return $"{skill?.DisplayName} - 최대 단계";
                case LearnCheck.Prerequisite:
                    return skill?.Prerequisite != null
                        ? $"먼저 {skill.Prerequisite.DisplayName} {skill.PrerequisiteLevel}단계가 필요하다"
                        : "선행 스킬이 필요하다";
                case LearnCheck.NotEnoughPoints: return $"SP가 모자란다 (필요 {NextCost(skill)})";
                default: return string.Empty;
            }
        }
    }
}
