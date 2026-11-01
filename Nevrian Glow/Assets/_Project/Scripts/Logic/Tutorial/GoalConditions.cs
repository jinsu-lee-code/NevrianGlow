using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Tutorial
{
    /// <summary>오늘의 목표 조건이 묻는 게임 상태 (지금 상태 — 행동 기록이 아니라서 불러와도 같은 답).</summary>
    public interface IGoalWorld
    {
        /// <summary>상점 단계형 상품을 산 단계 수.</summary>
        int TiersBought(string productId);
        /// <summary>작물이 심겨 있는 밭 칸 수.</summary>
        int PlantedCells { get; }
        /// <summary>그 작물이 심긴 칸이 있는지.</summary>
        bool HasPlanted(string cropId);
        /// <summary>그 작물에 비료를 준 칸이 있는지.</summary>
        bool HasFertilized(string cropId);
        /// <summary>확장 밭(시작 밭 밖)에 작물이 심긴 칸이 있는지.</summary>
        bool SitePlanted { get; }
        /// <summary>그 작물을 한 번이라도 수확했는지.</summary>
        bool HasHarvested(string cropId);
        /// <summary>오늘 장사를 마쳤는지 (정산까지).</summary>
        bool SalesDoneToday { get; }
    }

    /// <summary>
    /// 오늘의 목표 조건 (튜토리얼 v3 5절, daily_goals.csv 대기 — 개발이 v3 표로 만든 데이터). 문법:
    /// <c>조건 || 조건</c> (하나라도), 조건 = <c>bought:상품[,상품…]:단계</c>(모두) · <c>bought_any:상품,상품:단계</c>(하나라도) ·
    /// <c>planted_total:칸</c> · <c>planted:작물</c> · <c>fertilized:작물</c> · <c>site_planted</c> · <c>harvested:작물</c> · <c>sales_done</c>.
    /// 모르는 조건은 거짓 (데이터 오류는 검사기가 잡는다). 엔진 의존 없음.
    /// </summary>
    public static class GoalConditions
    {
        public static bool IsMet(string condition, IGoalWorld world)
        {
            if (string.IsNullOrWhiteSpace(condition) || world == null)
                return false;
            foreach (var part in condition.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries))
                if (IsMetOne(part.Trim(), world))
                    return true;
            return false;
        }

        /// <summary>문법이 맞는지 (데이터 검사 · 테스트).</summary>
        public static bool IsValid(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return false;
            foreach (var part in condition.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries))
                if (!TryParse(part.Trim(), out _, out _, out _))
                    return false;
            return true;
        }

        private static bool IsMetOne(string text, IGoalWorld world)
        {
            if (!TryParse(text, out string kind, out var ids, out int n))
                return false;
            switch (kind)
            {
                case "bought":
                    foreach (var id in ids)
                        if (world.TiersBought(id) < n)
                            return false;
                    return true;
                case "bought_any":
                    foreach (var id in ids)
                        if (world.TiersBought(id) >= n)
                            return true;
                    return false;
                case "planted_total": return world.PlantedCells >= n;
                case "planted": return world.HasPlanted(ids[0]);
                case "fertilized": return world.HasFertilized(ids[0]);
                case "site_planted": return world.SitePlanted;
                case "harvested": return world.HasHarvested(ids[0]);
                case "sales_done": return world.SalesDoneToday;
                default: return false;
            }
        }

        // kind[:ids][:n] — ids는 쉼표로 여럿
        private static bool TryParse(string text, out string kind, out List<string> ids, out int n)
        {
            ids = new List<string>();
            n = 1;
            var parts = text.Split(':');
            kind = parts[0].Trim();
            switch (kind)
            {
                case "bought":
                case "bought_any":
                    if (parts.Length != 3 || !int.TryParse(parts[2], out n) || n < 1)
                        return false;
                    foreach (var id in parts[1].Split(','))
                        if (!string.IsNullOrWhiteSpace(id))
                            ids.Add(id.Trim());
                    return ids.Count > 0;
                case "planted_total":
                    return parts.Length == 2 && int.TryParse(parts[1], out n) && n >= 1;
                case "planted":
                case "fertilized":
                case "harvested":
                    if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
                        return false;
                    ids.Add(parts[1].Trim());
                    return true;
                case "site_planted":
                case "sales_done":
                    return parts.Length == 1;
                default:
                    return false;
            }
        }
    }
}
