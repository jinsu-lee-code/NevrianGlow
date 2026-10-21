using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Game
{
    /// <summary>
    /// 한 판의 날마다 정산 모음 — 데모 종료 화면(3-4) 요약. 같은 날을 다시 기록하면 덮어쓴다(화면을 다시 열어도 두 번 세지 않음).
    /// 엔진 의존 없음 — 저장(6-1)은 날마다 정산 숫자 그대로.
    /// </summary>
    public sealed class RunSummary
    {
        private readonly SortedDictionary<int, SettlementReport> days = new SortedDictionary<int, SettlementReport>();

        public void Record(SettlementReport report) => days[report.Day] = report;

        /// <summary>기록을 비운다 (불러오기 6-1).</summary>
        public void Clear() => days.Clear();

        public int DaysRecorded => days.Count;
        public IEnumerable<SettlementReport> Days => days.Values;

        public int TotalCustomers => Sum(r => r.Customers);
        public int TotalItemsSold => Sum(r => r.ItemsSold);
        public int TotalRevenue => Sum(r => r.Revenue);
        public int TotalSpent => Sum(r => r.Spent);
        public int TotalNetProfit => TotalRevenue - TotalSpent;

        /// <summary>매출이 가장 많은 날 (같으면 앞 날). 매출이 있는 날이 없으면 false.</summary>
        public bool TryBestDay(out SettlementReport best)
        {
            best = default;
            bool found = false;
            foreach (var r in days.Values)
                if (r.Revenue > 0 && (!found || r.Revenue > best.Revenue))
                {
                    best = r;
                    found = true;
                }
            return found;
        }

        /// <summary>데모 종료 화면 본문. harvestedNames = 한 번이라도 수확한 작물 이름.
        /// 게임 화면 글자라 "—" 대신 "-"를 쓴다 (글꼴에 없음).</summary>
        public List<string> Lines(int level, int money, IReadOnlyList<string> harvestedNames)
        {
            var lines = new List<string>
            {
                $"손님 {TotalCustomers}명 · 판매 {TotalItemsSold}개",
                $"매출 합계 +{TotalRevenue:N0}달러 · 지출 합계 -{TotalSpent:N0}달러",
                $"순이익 합계 {Signed(TotalNetProfit)}달러",
            };
            if (TryBestDay(out var best))
                lines.Add($"가장 많이 판 날: {best.Day}일차 ({best.Revenue:N0}달러)");
            lines.Add($"레벨 {level} · 보유 {money:N0}달러");
            lines.Add(harvestedNames != null && harvestedNames.Count > 0
                ? $"수확한 작물: {string.Join(", ", harvestedNames)}"
                : "수확한 작물 없음");
            return lines;
        }

        /// <summary>데모 종료 화면을 보일 날인지 (lastDay 0 = 없음).</summary>
        public static bool IsDemoLastDay(int day, int lastDay) => lastDay > 0 && day == lastDay;

        private int Sum(System.Func<SettlementReport, int> pick)
        {
            int total = 0;
            foreach (var r in days.Values)
                total += pick(r);
            return total;
        }

        private static string Signed(int value) => value >= 0 ? $"+{value:N0}" : $"-{-(long)value:N0}";
    }
}
