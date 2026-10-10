using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NevrianGlowFarm.Logic.Game
{
    /// <summary>직접 플레이 하루 기록 (4-2): 단계별 걸린 시간 + 그날 정산 · 청결도 · 밭.</summary>
    public sealed class PlayDay
    {
        public int Day { get; set; }
        /// <summary>단계 → 그 단계에 머문 실제 시간 (초).</summary>
        public Dictionary<DayPhase, double> Seconds { get; } = new Dictionary<DayPhase, double>();
        public SettlementReport? Settlement { get; set; }
        /// <summary>장사 시작 때 확정된 청결도 (0~1, 장사를 안 했으면 null).</summary>
        public double? Cleanliness { get; set; }
        /// <summary>그날 정산 때 작물이 있는 칸 / 열린 칸.</summary>
        public int CropCells { get; set; }
        public int OpenCells { get; set; }
        /// <summary>정산 때 보유 달러 · SP.</summary>
        public int Money { get; set; }
        public int SkillPoints { get; set; }

        public double TotalSeconds
        {
            get
            {
                double t = 0;
                foreach (var s in Seconds.Values)
                    t += s;
                return t;
            }
        }
    }

    /// <summary>
    /// 직접 플레이 세션 기록 (TASKS 4-2) — 측정 봇 리포트(4-1)와 같은 숫자를 사람 플레이로 남기고, 봇이 못 재는 "단계별 걸린 시간"을 더한다.
    /// 엔진 의존 없음. 어댑터(PlaytestRecorder)가 단계 전환 때 시간을 더하고 날이 끝나면 파일로 쓴다.
    /// </summary>
    public sealed class PlaySessionLog
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly DayPhase[] Columns = { DayPhase.Shop, DayPhase.Farming, DayPhase.CleanDisplay, DayPhase.Sales, DayPhase.Settlement };

        private readonly SortedDictionary<int, PlayDay> days = new SortedDictionary<int, PlayDay>();

        public IEnumerable<PlayDay> Days => days.Values;

        public PlayDay DayOf(int day)
        {
            if (!days.TryGetValue(day, out var d))
                days[day] = d = new PlayDay { Day = day };
            return d;
        }

        /// <summary>단계에 머문 시간을 더한다 (같은 단계에 다시 오면 쌓인다). 음수는 무시.</summary>
        public void AddTime(int day, DayPhase phase, double seconds)
        {
            if (seconds <= 0 || day <= 0)
                return;
            var d = DayOf(day);
            d.Seconds[phase] = (d.Seconds.TryGetValue(phase, out var s) ? s : 0) + seconds;
        }

        /// <summary>마크다운 (제목 · 일별 표 · 합계). 시간은 분:초.</summary>
        public string Markdown(string title)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {title}");
            sb.AppendLine();
            sb.Append("| 일차 |");
            foreach (var p in Columns)
                sb.Append($" {DayPhaseNames.ToKorean(p)} |");
            sb.AppendLine(" 하루 | 청결도 | 손님 | 판매 | 매출 | 지출 | 순이익 | 보유 달러 | 레벨 · SP | 작물 칸 / 열린 칸 |");
            sb.Append("|---|");
            foreach (var _ in Columns)
                sb.Append("---|");
            sb.AppendLine("---|---|---|---|---|---|---|---|---|---|");

            double total = 0;
            foreach (var d in days.Values)
            {
                sb.Append($"| {d.Day} |");
                foreach (var p in Columns)
                    sb.Append($" {Time(d.Seconds.TryGetValue(p, out var s) ? s : 0)} |");
                total += d.TotalSeconds;
                string clean = d.Cleanliness.HasValue ? d.Cleanliness.Value.ToString("P0", Inv) : "-";
                if (d.Settlement.HasValue)
                {
                    var r = d.Settlement.Value;
                    sb.AppendLine($" {Time(d.TotalSeconds)} | {clean} | {r.Customers} | {r.ItemsSold} | {r.Revenue} | {r.Spent} | {r.NetProfit} | {d.Money} | Lv {r.Level} · {d.SkillPoints} | {d.CropCells} / {d.OpenCells} |");
                }
                else
                    sb.AppendLine($" {Time(d.TotalSeconds)} | {clean} | - | - | - | - | - | - | - | - |");
            }
            sb.AppendLine();
            sb.AppendLine($"합계 플레이 시간: {Time(total)}");
            return sb.ToString();
        }

        public static string Time(double seconds)
        {
            int s = (int)Math.Round(Math.Max(0, seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
