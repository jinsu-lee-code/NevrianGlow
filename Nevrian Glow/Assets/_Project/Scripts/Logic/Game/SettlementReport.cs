using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Game
{
    /// <summary>
    /// 하루 정산 (GDD 4-1: 손님 · 판매 · 매출 · 지출 · 경험치 확인). 순이익 = 매출 − 오늘 상점 지출.
    /// 화면은 Lines()를 그대로 보여 준다 — 측정 봇 · 테스트도 같은 문장을 쓴다.
    /// </summary>
    public readonly struct SettlementReport
    {
        public int Day { get; }
        /// <summary>오늘 장사를 했는지 (디버그로 장사 없이 정산에 온 경우 false).</summary>
        public bool HasSales { get; }
        public int Customers { get; }
        public int Buyers { get; }
        public int EmptyHanded { get; }
        public int ItemsSold { get; }
        public int Revenue { get; }
        /// <summary>작물 id → 판매 개수.</summary>
        public IReadOnlyDictionary<string, int> Sold { get; }
        /// <summary>오늘 상점에서 쓴 달러.</summary>
        public int Spent { get; }
        public ExpGain Exp { get; }
        /// <summary>정산 시점 레벨.</summary>
        public int Level { get; }
        public int NetProfit => Revenue - Spent;

        public SettlementReport(int day, bool hasSales, int customers, int buyers, int emptyHanded, int itemsSold, int revenue,
            IReadOnlyDictionary<string, int> sold, int spent, ExpGain exp, int level)
        {
            Day = day;
            HasSales = hasSales;
            Customers = hasSales ? customers : 0;
            Buyers = hasSales ? buyers : 0;
            EmptyHanded = hasSales ? emptyHanded : 0;
            ItemsSold = hasSales ? itemsSold : 0;
            Revenue = hasSales ? revenue : 0;
            Sold = hasSales && sold != null ? sold : new Dictionary<string, int>();
            Spent = Math.Max(0, spent);
            Exp = hasSales ? exp : default;
            Level = level;
        }

        /// <summary>정산 화면 본문. nameOf = 작물 id → 표시 이름 (없으면 id).
        /// 게임 화면 글자라 "—" 대신 "-"를 쓴다 (글꼴에 없음).</summary>
        public List<string> Lines(Func<string, string> nameOf)
        {
            var lines = new List<string>();
            if (HasSales)
            {
                lines.Add($"손님 {Customers}명 (구매 {Buyers} · 빈손 {EmptyHanded})");
                lines.Add(ItemsSold > 0 ? $"판매 {ItemsSold}개: {SoldText(nameOf)}" : "판매 0개");
            }
            else
                lines.Add("오늘은 장사 기록이 없음");
            lines.Add($"매출 +{Revenue:N0}달러");
            lines.Add($"지출 -{Spent:N0}달러 (상점)");
            lines.Add($"순이익 {Signed(NetProfit)}달러");
            string exp = $"경험치 +{Exp.Exp:N0} (Lv {Level})";
            if (Exp.LeveledUp)
                exp += $" · 레벨 업! SP +{Exp.SkillPointsGained}";
            lines.Add(exp);
            return lines;
        }

        /// <summary>판매 목록: 많이 판 것부터, 같으면 id 순 (같은 결과면 같은 문장).</summary>
        private string SoldText(Func<string, string> nameOf)
        {
            var items = new List<KeyValuePair<string, int>>(Sold);
            items.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
            var parts = new List<string>(items.Count);
            foreach (var pair in items)
            {
                string name = nameOf != null ? nameOf(pair.Key) : null;
                parts.Add($"{(string.IsNullOrEmpty(name) ? pair.Key : name)} ×{pair.Value}");
            }
            return string.Join(", ", parts);
        }

        private static string Signed(int value) => value >= 0 ? $"+{value:N0}" : $"-{-(long)value:N0}";
    }
}
