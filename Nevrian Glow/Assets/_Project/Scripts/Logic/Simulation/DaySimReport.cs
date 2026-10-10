using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NevrianGlowFarm.Logic.Simulation
{
    /// <summary>
    /// 측정 봇 리포트 (BALANCE 4번 6 · 7번 실험 표 형식). 마크다운 문자열 — 에디터 메뉴가 파일로 쓴다.
    /// 숫자는 시드 전체의 평균 (최소 ~ 최대). 첫 시드 한 판은 날마다 자세히.
    /// </summary>
    public static class DaySimReport
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>BALANCE 7번 실험 표 한 줄 값 (마지막 날 평균).</summary>
        public readonly struct Summary
        {
            public double Level { get; }
            public double SkillPoints { get; }
            public double Money { get; }
            public double GoalDaysMet { get; }
            public int GoalDays { get; }

            public Summary(double level, double skillPoints, double money, double goalDaysMet, int goalDays)
            {
                Level = level;
                SkillPoints = skillPoints;
                Money = money;
                GoalDaysMet = goalDaysMet;
                GoalDays = goalDays;
            }
        }

        public static Summary Summarize(IReadOnlyList<SimRun> runs)
        {
            if (runs == null || runs.Count == 0 || runs[0].Days.Count == 0)
                return default;
            var last = runs.Select(r => r.Days[r.Days.Count - 1]).ToList();
            int goalDays = runs[0].Days.Count(d => d.Goals.Count > 0);
            return new Summary(last.Average(d => d.Level), last.Average(d => d.SkillPoints), last.Average(d => d.Money),
                runs.Average(r => r.GoalDaysMet), goalDays);
        }

        /// <summary>리포트 본문. nameOf = 작물 id → 표시 이름 (없으면 id). notes = 맨 위에 붙일 조건 설명 줄.</summary>
        public static string Markdown(DaySimConfig config, IReadOnlyList<SimRun> runs, Func<string, string> nameOf, IEnumerable<string> notes = null)
        {
            string Name(string id) => string.IsNullOrEmpty(id) ? "모든 작물" : (nameOf?.Invoke(id) ?? id);
            var sb = new StringBuilder();
            sb.AppendLine($"# 측정 봇 리포트 (1~{config.Days}일차 × 시드 {runs.Count})");
            sb.AppendLine();
            sb.AppendLine($"- 조건: 시작 {config.StartMoney}달러 · 밭 {config.StartPlots}구획({config.StartPlots * config.CellsPerPlot}칸) · 진열 {config.ShelfSlots}칸 × {config.ShelfItemsPerSlot}개 · 청결도 {config.Cleanliness:P0} · 손님 보너스 {config.CustomerBonus:P0}");
            sb.AppendLine($"- 시작 씨앗: {Counts(config.StartSeeds, Name)} / 시작 수확물: {Counts(config.StartCrops, Name)}");
            sb.AppendLine($"- 작물 (칸당 일일 이익 순): {string.Join(", ", config.Crops.OrderByDescending(c => c.ProfitPerCellPerDay).Select(c => $"{Name(c.Id)} {c.ProfitPerCellPerDay.ToString("0.#", Inv)}"))}");
            sb.AppendLine($"- 스킬 순서: {string.Join(" → ", config.SkillPriority.Select(s => $"{s.Id}({string.Join("/", s.Costs)} SP)"))}");
            if (notes != null)
                foreach (var n in notes)
                    sb.AppendLine($"- {n}");
            sb.AppendLine();

            var s = Summarize(runs);
            sb.AppendLine("## 요약 (BALANCE 7번 실험 표 형식)");
            sb.AppendLine($"| {config.Days}일차 레벨 | {config.Days}일차 SP | {config.Days}일차 보유 달러 | 목표 달성 일차 수 |");
            sb.AppendLine("|---|---|---|---|");
            sb.AppendLine($"| {F(s.Level)} | {F(s.SkillPoints)} | {F(s.Money)} | {F(s.GoalDaysMet)} / {s.GoalDays} |");
            sb.AppendLine();

            sb.AppendLine("## 일별 (시드 평균, 괄호는 최소 ~ 최대)");
            sb.AppendLine("| 일차 | 손님 | 판매 | 매출 | 지출 | 보유 달러 | 레벨 | 남은 SP | 열린 칸 | 작물 있는 칸 | 미판매 재고 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            for (int i = 0; i < config.Days; i++)
            {
                var days = runs.Where(r => r.Days.Count > i).Select(r => r.Days[i]).ToList();
                if (days.Count == 0)
                    break;
                sb.AppendLine($"| {i + 1} | {Stat(days, d => d.Customers)} | {Stat(days, d => d.ItemsSold)} | {Stat(days, d => d.Revenue)} | {Stat(days, d => d.Spent)} | {Stat(days, d => d.Money)} | {Stat(days, d => d.Level)} | {Stat(days, d => d.SkillPoints)} | {Stat(days, d => d.OpenCells)} | {Stat(days, d => d.PlantedCells)} | {Stat(days, d => d.Unsold)} |");
            }
            sb.AppendLine();

            sb.AppendLine("## GDD 6장 일차별 목표 달성률");
            sb.AppendLine("| 일차 | 목표 | 판정 | 달성 시드 |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var goal in config.Goals.OrderBy(g => g.Day))
            {
                int met = runs.Count(r => r.Days.Where(d => d.Day == goal.Day).SelectMany(d => d.Goals).Any(g => g.Key == goal && g.Value));
                sb.AppendLine($"| {goal.Day} | {goal.Label} | {GoalText(goal, Name)} | {met} / {runs.Count} |");
            }
            sb.AppendLine();

            if (runs.Count > 0)
            {
                var first = runs[0];
                sb.AppendLine($"## 한 판 자세히 (시드 {first.Seed})");
                sb.AppendLine("| 일차 | 상점 지출 | 심음 | 수확 | 매출 | 보유 달러 | 레벨 · SP | 배운 스킬 | 해금 작물 | 목표 |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
                foreach (var d in first.Days)
                {
                    string goals = d.Goals.Count == 0 ? "-" : (d.AllGoalsMet ? "달성" : "미달: " + string.Join(", ", d.Goals.Where(g => !g.Value).Select(g => g.Key.Label)));
                    sb.AppendLine($"| {d.Day} | {d.Spent} | {Counts(d.Planted, Name)} | {Counts(d.Harvested, Name)} | {d.Revenue} | {d.Money} | Lv {d.Level} · {d.SkillPoints} | {(d.SkillsLearned.Count == 0 ? "-" : string.Join(", ", d.SkillsLearned))} | {(d.Unlocked.Count == 0 ? "-" : string.Join(", ", d.Unlocked.OrderBy(x => x, StringComparer.Ordinal).Select(Name)))} | {goals} |");
                }
            }
            return sb.ToString();
        }

        private static string GoalText(SimGoal goal, Func<string, string> name)
        {
            switch (goal.Kind)
            {
                case SimGoalKind.PlantedAtLeast: return $"{name(goal.CropId)} 새로 심은 칸 ≥ {goal.Amount}";
                case SimGoalKind.HarvestedAtLeast: return $"{name(goal.CropId)} 수확 ≥ {goal.Amount}개";
                case SimGoalKind.CropUnlocked: return $"{name(goal.CropId)} 씨앗이 상점에 보임";
                case SimGoalKind.PlantedMoreThanPreviousDay: return $"{name(goal.CropId)} 새로 심은 칸 > 전날";
                default: return goal.Kind.ToString();
            }
        }

        private static string Stat(List<SimDay> days, Func<SimDay, int> pick)
        {
            int min = days.Min(pick), max = days.Max(pick);
            string avg = F(days.Average(pick));
            return min == max ? avg : $"{avg} ({min} ~ {max})";
        }

        private static string F(double v) => v.ToString("0.#", Inv);

        private static string Counts(IReadOnlyDictionary<string, int> counts, Func<string, string> name) =>
            counts == null || counts.Count == 0 ? "-" : string.Join(", ", counts.Where(p => p.Value > 0).OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{name(p.Key)} {p.Value}"));
    }
}
