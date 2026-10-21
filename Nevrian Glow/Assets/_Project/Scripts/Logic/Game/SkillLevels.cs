using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Game
{
    /// <summary>
    /// 스킬 id → 배운 단계 (0 = 안 배움). 상점 해금 조건(3-3)이 읽는다. SP 소비 · 선행 조건 · 효과는 SkillSystem(4-3).
    /// 엔진 의존 없음 — 저장(6-1)은 id · 단계 그대로.
    /// </summary>
    public sealed class SkillLevels
    {
        private readonly Dictionary<string, int> levels = new Dictionary<string, int>();

        public int LevelOf(string skillId) => skillId != null && levels.TryGetValue(skillId, out int level) ? level : 0;

        /// <summary>단계를 정한다 (0 미만은 0). 바뀌었으면 true.</summary>
        public bool SetLevel(string skillId, int level)
        {
            if (string.IsNullOrEmpty(skillId))
                return false;
            level = Math.Max(0, level);
            if (LevelOf(skillId) == level)
                return false;
            if (level == 0)
                levels.Remove(skillId);
            else
                levels[skillId] = level;
            return true;
        }

        public IReadOnlyDictionary<string, int> All => levels;

        /// <summary>모두 0단계로 (불러오기 6-1).</summary>
        public void Clear() => levels.Clear();
    }

    /// <summary>
    /// 지금까지 한 일 기록 — 해금 조건 "첫 수확"(BALANCE 3-8 추천값) 등. 엔진 의존 없음.
    /// </summary>
    public sealed class GameRecord
    {
        private readonly HashSet<string> harvested = new HashSet<string>();

        public bool HasHarvested(string cropId) => cropId != null && harvested.Contains(cropId);

        /// <summary>처음 수확이면 true.</summary>
        public bool RecordHarvest(string cropId) => cropId != null && harvested.Add(cropId);

        public IEnumerable<string> Harvested => harvested;

        /// <summary>기록을 비운다 (불러오기 6-1).</summary>
        public void Clear() => harvested.Clear();
    }
}
