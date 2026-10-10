using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Game
{
    /// <summary>레벨 규칙 값 (ProgressionData에서 옮겨 온다, BALANCE 2-7).</summary>
    public readonly struct LevelCurve
    {
        public double ExpPerRevenue { get; }
        public int LevelUpBase { get; }
        public int LevelUpPerLevel { get; }
        public int SkillPointsPerLevel { get; }

        public LevelCurve(double expPerRevenue, int levelUpBase, int levelUpPerLevel, int skillPointsPerLevel)
        {
            ExpPerRevenue = Math.Max(0, expPerRevenue);
            LevelUpBase = Math.Max(1, levelUpBase);
            LevelUpPerLevel = Math.Max(0, levelUpPerLevel);
            SkillPointsPerLevel = Math.Max(0, skillPointsPerLevel);
        }

        /// <summary>지금 레벨에서 다음 레벨까지 필요한 경험치 = 기본 + 레벨당 증가 × 현재 레벨.</summary>
        public int RequiredFor(int level) => LevelUpBase + LevelUpPerLevel * Math.Max(1, level);

        /// <summary>매출 → 경험치 (내림). 경험치 = 매출 × 1 (GDD 4-6).</summary>
        public int ExpFromRevenue(int revenue) => revenue <= 0 ? 0 : (int)Math.Floor(revenue * ExpPerRevenue);
    }

    /// <summary>경험치를 받은 결과.</summary>
    public readonly struct ExpGain
    {
        public int Exp { get; }
        public int LevelsGained { get; }
        public int SkillPointsGained { get; }
        public bool LeveledUp => LevelsGained > 0;

        public ExpGain(int exp, int levelsGained, int skillPointsGained)
        {
            Exp = exp;
            LevelsGained = levelsGained;
            SkillPointsGained = skillPointsGained;
        }
    }

    /// <summary>레벨 표 한 줄 (인스펙터 · 측정 봇).</summary>
    public readonly struct LevelRow
    {
        public int Level { get; }
        public int ToNext { get; }
        public int CumulativeExp { get; }
        public int CumulativeSkillPoints { get; }

        public LevelRow(int level, int toNext, int cumulativeExp, int cumulativeSkillPoints)
        {
            Level = level;
            ToNext = toNext;
            CumulativeExp = cumulativeExp;
            CumulativeSkillPoints = cumulativeSkillPoints;
        }
    }

    /// <summary>
    /// 경험치 · 레벨 (GDD 4-6, BALANCE 2-7 · 3-2). 받은 경험치로 필요량을 넘으면 레벨업을 반복하고, 남은 경험치는 이어진다.
    /// 레벨업마다 SP — SP 자체는 Wallet이 가진다. 엔진 의존 없음.
    /// </summary>
    public sealed class Progression
    {
        public const int FirstLevel = 1;

        public Progression(LevelCurve curve) : this(curve, FirstLevel, 0) { }

        /// <summary>저장에서 불러올 때. 경험치는 지금 레벨 안에서의 값.</summary>
        public Progression(LevelCurve curve, int level, int exp)
        {
            if (level < FirstLevel) throw new ArgumentOutOfRangeException(nameof(level), level, $"레벨은 {FirstLevel} 이상");
            if (exp < 0) throw new ArgumentOutOfRangeException(nameof(exp), exp, "0 이상");
            Curve = curve;
            Level = level;
            Exp = exp;
        }

        public LevelCurve Curve { get; }
        public int Level { get; private set; }
        /// <summary>지금 레벨 안에서 모은 경험치 (0 ~ 필요량 미만).</summary>
        public int Exp { get; private set; }
        /// <summary>다음 레벨까지 필요한 경험치.</summary>
        public int Required => Curve.RequiredFor(Level);

        /// <summary>경험치를 더한다. 필요량을 넘을 때마다 레벨업 (한 번에 여러 번 가능). 0 이하면 아무 일도 없다.</summary>
        public ExpGain AddExp(int amount)
        {
            if (amount <= 0)
                return new ExpGain(0, 0, 0);
            Exp += amount;
            int levels = 0;
            while (Exp >= Required)
            {
                Exp -= Required;
                Level++;
                levels++;
            }
            return new ExpGain(amount, levels, levels * Curve.SkillPointsPerLevel);
        }

        /// <summary>매출로 경험치를 받는다 (정산).</summary>
        public ExpGain AddRevenue(int revenue) => AddExp(Curve.ExpFromRevenue(revenue));

        /// <summary>레벨 1부터 maxLevel까지 표 (BALANCE 3-2와 같은 모양).</summary>
        public static List<LevelRow> Table(LevelCurve curve, int maxLevel)
        {
            var rows = new List<LevelRow>();
            int cumulative = 0;
            for (int level = FirstLevel; level <= maxLevel; level++)
            {
                int toNext = curve.RequiredFor(level);
                rows.Add(new LevelRow(level, toNext, cumulative, (level - FirstLevel) * curve.SkillPointsPerLevel));
                cumulative += toNext;
            }
            return rows;
        }
    }
}
