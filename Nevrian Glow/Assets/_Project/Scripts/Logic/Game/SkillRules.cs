using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Game
{
    /// <summary>스킬 한 개의 배우기 규칙 값 (SkillData.ToSpec). 단계 수 = 단계별 비용 개수.</summary>
    public readonly struct SkillSpec
    {
        public string Id { get; }
        public IReadOnlyList<int> Costs { get; }
        /// <summary>선행 스킬 id (없으면 null).</summary>
        public string PrerequisiteId { get; }
        /// <summary>선행 스킬에 필요한 단계 (1 이상).</summary>
        public int PrerequisiteLevel { get; }

        public int MaxLevel => Costs != null ? Costs.Count : 0;

        public SkillSpec(string id, IReadOnlyList<int> costs, string prerequisiteId = null, int prerequisiteLevel = 1)
        {
            Id = id;
            Costs = costs ?? Array.Empty<int>();
            PrerequisiteId = string.IsNullOrEmpty(prerequisiteId) ? null : prerequisiteId;
            PrerequisiteLevel = Math.Max(1, prerequisiteLevel);
        }
    }

    /// <summary>배우기 판정 결과. enum은 끝에만 추가.</summary>
    public enum LearnCheck
    {
        Ok,
        MaxLevel,
        Prerequisite,
        NotEnoughPoints,
    }

    /// <summary>스킬 배우기 규칙 (TASKS 4-3, BALANCE 9-3). 엔진 의존 없음.</summary>
    public static class SkillRules
    {
        /// <summary>다음 단계 비용 (SP). 최대 단계면 0.</summary>
        public static int NextCost(SkillSpec spec, int currentLevel) =>
            currentLevel >= 0 && currentLevel < spec.MaxLevel ? Math.Max(0, spec.Costs[currentLevel]) : 0;

        /// <summary>
        /// 다음 단계를 배울 수 있는지. 확인 순서: 최대 단계 → 선행 스킬 → SP.
        /// levelOf = 스킬 id → 지금 단계 (선행 확인용).
        /// </summary>
        public static LearnCheck CanLearn(SkillSpec spec, int currentLevel, Func<string, int> levelOf, int skillPoints)
        {
            if (currentLevel >= spec.MaxLevel)
                return LearnCheck.MaxLevel;
            if (spec.PrerequisiteId != null && (levelOf == null || levelOf(spec.PrerequisiteId) < spec.PrerequisiteLevel))
                return LearnCheck.Prerequisite;
            if (skillPoints < NextCost(spec, currentLevel))
                return LearnCheck.NotEnoughPoints;
            return LearnCheck.Ok;
        }
    }

    /// <summary>능력치 변경 방식. enum은 끝에만 추가.</summary>
    public enum ModifierOp
    {
        /// <summary>기본값에 더한다.</summary>
        Add,
        /// <summary>(기본값 + 더하기 합)에 곱한다.</summary>
        Multiply,
        /// <summary>기본값을 이 값으로 바꾼다 (기획 skill_effects op = set, 예: 핫바 칸 5). 여럿이면 가장 큰 값.</summary>
        Set,
    }

    /// <summary>능력치 하나에 거는 변경 (스킬 단계 효과 등).</summary>
    public readonly struct StatModifier
    {
        public string StatId { get; }
        public ModifierOp Op { get; }
        public double Value { get; }

        public StatModifier(string statId, ModifierOp op, double value)
        {
            StatId = statId;
            Op = op;
            Value = value;
        }
    }

    /// <summary>
    /// 능력치 계산 (노션 GDD 12장 StatSystem · Modifier): 결과 = (기본값 또는 Set 값 + 더하기 합) × 곱하기 곱.
    /// 같은 능력치를 여러 스킬이 바꾸면 더하기는 더하고 곱하기는 곱하고, Set은 가장 큰 값이 기본값을 대신한다. 엔진 의존 없음.
    /// </summary>
    public static class StatCalculator
    {
        public static double Apply(string statId, double baseValue, IEnumerable<StatModifier> modifiers)
        {
            double add = 0.0, mul = 1.0;
            double? set = null;
            if (modifiers != null && statId != null)
            {
                foreach (var m in modifiers)
                {
                    if (m.StatId != statId)
                        continue;
                    switch (m.Op)
                    {
                        case ModifierOp.Add: add += m.Value; break;
                        case ModifierOp.Multiply: mul *= m.Value; break;
                        case ModifierOp.Set: set = set.HasValue ? Math.Max(set.Value, m.Value) : m.Value; break;
                    }
                }
            }
            return ((set ?? baseValue) + add) * mul;
        }

        /// <summary>정수 결과 (판매가 · 칸 수 · 성장일). 0.5 올림 (BALANCE 3-4와 같은 반올림).</summary>
        public static int ApplyRounded(string statId, int baseValue, IEnumerable<StatModifier> modifiers) =>
            (int)Math.Floor(Apply(statId, baseValue, modifiers) + 0.5 + 1e-9);

        /// <summary>
        /// 스킬 단계 효과: 단계별 값 목록에서 지금 단계의 값 (1단계 = 첫 값). 0단계 · 빈 목록이면 효과 없음(false).
        /// 값 목록보다 높은 단계면 마지막 값.
        /// </summary>
        public static bool ValueAtLevel(IReadOnlyList<double> values, int level, out double value)
        {
            value = 0.0;
            if (values == null || values.Count == 0 || level <= 0)
                return false;
            value = values[Math.Min(level, values.Count) - 1];
            return true;
        }
    }
}
