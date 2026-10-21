using System;
using System.Collections.Generic;
using NevrianGlowFarm.Logic.Game;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 스킬 하나 = 에셋 하나 (TASKS 4-3, BALANCE 9-3 · 2-9). 단계 수 = 단계별 비용 개수.
    /// 효과는 두 가지: 능력치 변경(modifiers) · 상점 상품 등장(상품 쪽 해금 조건이 이 스킬 id와 단계를 가리킨다 — 3-3 그대로).
    /// 스킬 추가는 에셋 추가만으로 (ContentDatabase 자동 수집, 스킬 화면은 탭 · 순서로 자동 배치).
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Skill", fileName = "skill_new")]
    public class SkillData : ContentData
    {
        [Serializable]
        public class ModifierEntry
        {
            [Tooltip("바꿀 능력치")]
            [SerializeField] private StatData stat;
            [Tooltip("Add = 기본값에 더함 · Multiply = 곱함")]
            [SerializeField] private ModifierOp op;
            [Tooltip("단계별 값 — 그 단계의 전체 효과 (1단계 = 첫 칸, 예: 판매가 1.05 / 1.10 / 1.15)")]
            [SerializeField] private List<float> values = new List<float>();

            public StatData Stat => stat;
            public ModifierOp Op => op;
            public IReadOnlyList<float> Values => values;
        }

        [Header("배치")]
        [Tooltip("스킬 화면 탭 (예: 농사 · 공통 · 청소 · 장사)")]
        [SerializeField] private string tab;
        [Tooltip("탭 안 순서 (작을수록 위)")]
        [SerializeField] private int order;
        [Tooltip("단계별 설명 (1단계 = 첫 줄). 비우면 효과 값으로 대신 적는다")]
        [SerializeField] private List<string> levelDescriptions = new List<string>();

        [Header("배우기")]
        [Tooltip("단계별 비용 (SP) — 개수 = 최대 단계")]
        [SerializeField] private List<int> costs = new List<int>();
        [Tooltip("선행 스킬 (없으면 비움)")]
        [SerializeField] private SkillData prerequisite;
        [Tooltip("선행 스킬에 필요한 단계")]
        [SerializeField, Min(1)] private int prerequisiteLevel = 1;

        [Header("효과")]
        [Tooltip("능력치 변경 — 상점 상품 등장은 상품 데이터의 해금 조건에서 이 스킬 id를 가리킨다")]
        [SerializeField] private List<ModifierEntry> modifiers = new List<ModifierEntry>();

        public string Tab => tab;
        public int Order => order;
        public IReadOnlyList<int> Costs => costs;
        public int MaxLevel => costs.Count;
        public SkillData Prerequisite => prerequisite;
        public int PrerequisiteLevel => prerequisiteLevel;
        public IReadOnlyList<ModifierEntry> Modifiers => modifiers;

        /// <summary>단계 설명 (level 1부터). 없으면 null.</summary>
        public string LevelDescription(int level) =>
            level >= 1 && level <= levelDescriptions.Count && !string.IsNullOrEmpty(levelDescriptions[level - 1]) ? levelDescriptions[level - 1] : null;

        /// <summary>Logic 배우기 규칙 값.</summary>
        public SkillSpec ToSpec() => new SkillSpec(Id, costs, prerequisite != null ? prerequisite.Id : null, prerequisiteLevel);

        /// <summary>이 단계에서 거는 능력치 변경 (0단계면 없음).</summary>
        public void CollectModifiers(int level, List<StatModifier> into)
        {
            foreach (var m in modifiers)
            {
                if (m == null || m.Stat == null)
                    continue;
                var values = new double[m.Values.Count];
                for (int i = 0; i < values.Length; i++)
                    values[i] = m.Values[i];
                if (StatCalculator.ValueAtLevel(values, level, out double v))
                    into.Add(new StatModifier(m.Stat.Id, m.Op, v));
            }
        }
    }
}
