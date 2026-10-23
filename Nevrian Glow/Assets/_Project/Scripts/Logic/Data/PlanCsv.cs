using System;
using System.Collections.Generic;
using NevrianGlowFarm.Logic.Game;

namespace NevrianGlowFarm.Logic.Data
{
    /// <summary>ID_MAP.md 한 줄: 기획 id ↔ 개발 id (결정 2026-10-21: 개발 id 유지, 대응표로 연결).</summary>
    public sealed class IdMapEntry
    {
        public string Kind { get; }
        /// <summary>기획 CSV의 id (없으면 null — 개발에만 있음).</summary>
        public string PlanId { get; }
        /// <summary>개발 에셋 id (없으면 null — 개발 에셋 없음).</summary>
        public string DevId { get; }
        public IdMapStatus Status { get; }
        /// <summary>변환 (능력치: "1+값 배율" 등). 없으면 빈 문자열.</summary>
        public string Conversion { get; }

        public IdMapEntry(string kind, string planId, string devId, IdMapStatus status, string conversion)
        {
            Kind = kind ?? string.Empty;
            PlanId = planId;
            DevId = devId;
            Status = status;
            Conversion = conversion ?? string.Empty;
        }
    }

    /// <summary>대응표 상태. enum은 끝에만 추가.</summary>
    public enum IdMapStatus
    {
        /// <summary>값을 가져와 쓴다.</summary>
        Linked,
        /// <summary>값은 가져오되 상점 · 스킬 화면에 숨긴다 (시스템 연결 전).</summary>
        Hidden,
        /// <summary>개발 에셋 없음 — 건너뛴다.</summary>
        None,
        /// <summary>개발에만 있는 에셋 — 숨긴다 (기획에 없음 · 폐기).</summary>
        DevOnly,
    }

    /// <summary>
    /// ID_MAP.md 표 (| 종류 | 기획 id | 개발 id | 상태 | 변환 | 메모 |). 사람이 읽는 문서가 곧 가져오기 설정이다.
    /// 표에 없는 기획 id는 개발 id가 같다고 본다. "-"는 없음.
    /// </summary>
    public sealed class IdMap
    {
        private readonly List<IdMapEntry> entries = new List<IdMapEntry>();

        public IReadOnlyList<IdMapEntry> Entries => entries;

        public static IdMap Parse(string markdown)
        {
            var map = new IdMap();
            if (string.IsNullOrEmpty(markdown))
                return map;
            foreach (var raw in markdown.Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith("|"))
                    continue;
                var cells = line.Trim('|').Split('|');
                if (cells.Length < 4)
                    continue;
                for (int i = 0; i < cells.Length; i++)
                    cells[i] = cells[i].Trim().Trim('`').Trim();
                if (cells[1].StartsWith("---") || cells[1] == "기획 id")
                    continue;
                if (!TryStatus(cells[3], out var status))
                    continue;
                map.entries.Add(new IdMapEntry(cells[0], NullIfDash(cells[1]), NullIfDash(cells[2]), status, cells.Length > 4 ? cells[4] : string.Empty));
            }
            return map;
        }

        /// <summary>기획 id의 줄 (종류를 주면 그 종류에서만). 없으면 null.</summary>
        public IdMapEntry Find(string planId, string kind = null)
        {
            foreach (var e in entries)
                if (e.PlanId == planId && (kind == null || e.Kind == kind))
                    return e;
            return null;
        }

        /// <summary>기획 id → 개발 id. 표에 없으면 같은 id, 표에 "없음"이면 null.</summary>
        public string DevIdOf(string planId, string kind = null)
        {
            if (string.IsNullOrEmpty(planId))
                return null;
            var e = Find(planId, kind);
            return e == null ? planId : e.DevId;
        }

        /// <summary>개발에만 있는 id들 (숨길 대상).</summary>
        public IEnumerable<IdMapEntry> DevOnly()
        {
            foreach (var e in entries)
                if (e.Status == IdMapStatus.DevOnly && e.DevId != null)
                    yield return e;
        }

        private static string NullIfDash(string s) => string.IsNullOrEmpty(s) || s == "-" || s == "—" ? null : s;

        private static bool TryStatus(string text, out IdMapStatus status)
        {
            switch (text)
            {
                case "연결": status = IdMapStatus.Linked; return true;
                case "숨김": status = IdMapStatus.Hidden; return true;
                case "없음": status = IdMapStatus.None; return true;
                case "개발만": status = IdMapStatus.DevOnly; return true;
                default: status = IdMapStatus.None; return false;
            }
        }
    }

    /// <summary>기획 CSV 칸 해석 · 개발 값으로 바꾸는 규칙 (TASKS 기획 확정 반영 2026-10-21). 엔진 의존 없음.</summary>
    public static class PlanCsv
    {
        /// <summary>"a;b;;c" → [a, b, c].</summary>
        public static List<string> SplitList(string text)
        {
            var list = new List<string>();
            foreach (var part in (text ?? string.Empty).Split(';'))
            {
                string p = part.Trim();
                if (p.Length > 0)
                    list.Add(p);
            }
            return list;
        }

        /// <summary>"1;2;3" → [1, 2, 3]. 숫자가 아니면 false.</summary>
        public static bool TryIntList(string text, out List<int> values)
        {
            values = new List<int>();
            foreach (var p in SplitList(text))
            {
                if (!CsvValue.TryInt(p, out int v))
                    return false;
                values.Add(v);
            }
            return true;
        }

        /// <summary>"skill_a:1;skill_b:2" → [(skill_a, 1), (skill_b, 2)]. 단계를 안 적으면 1.</summary>
        public static List<(string Id, int Level)> ParseRequirements(string text)
        {
            var list = new List<(string, int)>();
            foreach (var p in SplitList(text))
            {
                int colon = p.IndexOf(':');
                if (colon < 0)
                {
                    list.Add((p, 1));
                    continue;
                }
                string id = p.Substring(0, colon).Trim();
                int level = CsvValue.TryInt(p.Substring(colon + 1).Trim(), out int l) ? Math.Max(1, l) : 1;
                list.Add((id, level));
            }
            return list;
        }

        /// <summary>
        /// 단계형 해금 (skill_effects unlock_tier): 스킬 단계별 "살 수 있는 최대 상품 단계" → 상품 단계(0부터)마다 필요한 스킬 단계.
        /// 예: 가방 (1 → 2, 2 → 4, 3 → 7) → 0·1단계 = 1, 2·3단계 = 2, 4~6단계 = 3. 어느 스킬 단계로도 열리지 않는 상품 단계는 -1.
        /// </summary>
        public static int[] TierLevels(int tierCount, IEnumerable<(int SkillLevel, int MaxTier)> unlockTiers)
        {
            var levels = new int[Math.Max(0, tierCount)];
            var sorted = new List<(int SkillLevel, int MaxTier)>(unlockTiers ?? Array.Empty<(int, int)>());
            sorted.Sort((a, b) => a.SkillLevel.CompareTo(b.SkillLevel));
            for (int t = 0; t < levels.Length; t++)
            {
                levels[t] = -1;
                foreach (var u in sorted)
                    if (u.MaxTier >= t + 1)
                    {
                        levels[t] = u.SkillLevel;
                        break;
                    }
            }
            return levels;
        }

        /// <summary>밭 칸 → 개발 구획 (구획 하나 = cellsPerPlot칸). 나누어떨어지지 않으면 false.</summary>
        public static bool TryPlotsFromCells(int cells, int cellsPerPlot, out int plots)
        {
            plots = 0;
            if (cells <= 0 || cellsPerPlot <= 0 || cells % cellsPerPlot != 0)
                return false;
            plots = cells / cellsPerPlot;
            return true;
        }

        /// <summary>
        /// 기획 레벨 공식 "base + step × (레벨 − 1)" → 개발 LevelCurve "기본 + 증가 × 레벨" 값 (기본 = base − step).
        /// 예: 100 + 50 × (레벨 − 1) → 기본 50 · 증가 50.
        /// </summary>
        public static (int LevelUpBase, int LevelUpPerLevel) LevelCurveFromPlan(int planBase, int planStep) =>
            (Math.Max(1, planBase - planStep), Math.Max(0, planStep));

        public const string BonusToMultiplier = "1+값 배율";

        /// <summary>
        /// 기획 op(add · mul · set) + 값 → 개발 능력치 변경. conversion이 "1+값 배율"이면 보너스(add v)를 배율(× (1 + v))로 바꾼다
        /// (기획 sell_price_raw add 0.05 = 판매가 +5% → 개발 stat_sell_price × 1.05). 모르는 op면 false.
        /// </summary>
        public static bool TryConvertModifier(string op, double value, string conversion, out ModifierOp devOp, out double devValue)
        {
            devValue = value;
            devOp = ModifierOp.Add;
            switch ((op ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "add": devOp = ModifierOp.Add; break;
                case "mul": devOp = ModifierOp.Multiply; break;
                case "set": devOp = ModifierOp.Set; break;
                default: return false;
            }
            if (conversion == BonusToMultiplier && devOp == ModifierOp.Add)
            {
                devOp = ModifierOp.Multiply;
                devValue = 1.0 + value;
            }
            return true;
        }
    }
}
