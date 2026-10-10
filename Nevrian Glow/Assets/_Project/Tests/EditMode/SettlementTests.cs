using System.Collections.Generic;
using NevrianGlowFarm.Logic.Game;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>3-3b 정산: 순이익 = 매출 − 지출, 판매 목록 순서, 장사 없음, 레벨업 줄.</summary>
    public class SettlementTests
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "crop_a", "월광" },
            { "crop_b", "버섯꽃" },
        };

        private static string NameOf(string id) => Names.TryGetValue(id, out var n) ? n : null;

        private static SettlementReport Report(int revenue, int spent, Dictionary<string, int> sold, ExpGain exp = default) =>
            new SettlementReport(2, true, 8, 7, 1, 6, revenue, sold, spent, exp, 3);

        [Test]
        public void NetProfit_RevenueMinusSpent()
        {
            var sold = new Dictionary<string, int> { { "crop_a", 4 }, { "crop_b", 2 } };
            Assert.AreEqual(110, Report(140, 30, sold).NetProfit);
            var loss = Report(140, 690, sold);
            Assert.AreEqual(-550, loss.NetProfit);
            Assert.Contains("순이익 -550달러", loss.Lines(NameOf));
            Assert.Contains("순이익 +0달러", Report(30, 30, sold).Lines(NameOf));
        }

        [Test]
        public void Lines_SoldMostFirst_UnknownIdShown()
        {
            var sold = new Dictionary<string, int> { { "crop_b", 2 }, { "crop_x", 2 }, { "crop_a", 4 } };
            var lines = Report(140, 0, sold, new ExpGain(140, 0, 0)).Lines(NameOf);
            Assert.AreEqual("손님 8명 (구매 7 · 빈손 1)", lines[0]);
            Assert.AreEqual("판매 6개: 월광 ×4, 버섯꽃 ×2, crop_x ×2", lines[1], "많은 것부터, 같으면 id 순, 이름 없으면 id");
            Assert.AreEqual("매출 +140달러", lines[2]);
            Assert.AreEqual("지출 -0달러 (상점)", lines[3]);
            Assert.AreEqual("경험치 +140 (Lv 3)", lines[5]);
            foreach (var line in lines)
                StringAssert.DoesNotContain("—", line, "게임 화면 글자에 긴 줄표 금지 (글꼴에 없음)");
        }

        [Test]
        public void LevelUp_ShowsSkillPoints()
        {
            var lines = Report(240, 0, new Dictionary<string, int> { { "crop_a", 11 } }, new ExpGain(240, 2, 6)).Lines(NameOf);
            Assert.AreEqual("경험치 +240 (Lv 3) · 레벨 업! SP +6", lines[lines.Count - 1]);
        }

        [Test]
        public void NoSales_OnlySpending()
        {
            var r = new SettlementReport(4, false, 9, 9, 9, 9, 999, new Dictionary<string, int> { { "crop_a", 1 } }, 120, new ExpGain(999, 1, 3), 2);
            Assert.AreEqual(0, r.Revenue, "장사가 없으면 결과 값을 무시");
            Assert.AreEqual(0, r.Exp.Exp);
            Assert.AreEqual(-120, r.NetProfit);
            var lines = r.Lines(NameOf);
            Assert.AreEqual("오늘은 장사 기록이 없음", lines[0]);
            Assert.AreEqual("경험치 +0 (Lv 2)", lines[lines.Count - 1]);
        }
    }
}
