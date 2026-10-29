using System.Collections.Generic;
using NevrianGlowFarm.Logic.Farm;
using NevrianGlowFarm.Logic.Shop;

namespace NevrianGlowFarm.Logic.Text
{
    /// <summary>
    /// 화면 글자 조립 규칙 (5-4c 툴팁 · 목록 둘째 줄). 엔진 의존 없음 — 게임 화면 글자라 "—"를 쓰지 않는다 (TASKS 트러블슈팅).
    /// 수치는 데이터에서 받아 그대로 적고, 조각을 " · "로 잇는다.
    /// </summary>
    public static class UiText
    {
        public const string Separator = " · ";

        /// <summary>작물 설명 한 줄: 성장일 · 물 · 수확량 · 재수확 · 판매가(0이면 생략).</summary>
        public static string Crop(CropSpec spec, int sellPrice)
        {
            var parts = new List<string>
            {
                $"성장 {spec.GrowDays}일",
                spec.NeedsWater ? "물 필요" : "물 없이 자람",
                $"수확 {spec.HarvestYield}개",
            };
            if (spec.RegrowDays > 0 && spec.MaxHarvests > 1)
                parts.Add($"{spec.RegrowDays}일마다 다시 수확 (총 {spec.MaxHarvests}회)");
            if (sellPrice > 0)
                parts.Add($"판매가 {sellPrice:N0}달러");
            return string.Join(Separator, parts);
        }

        /// <summary>
        /// 상품 상태 한 줄 (목록 둘째 줄). 빈 문자열이면 적을 것이 없음.
        /// 잠긴 상품은 입고 수를 적지 않는다 (해금 전이라 0개로 보여 헷갈림). stockLeft가 음수면 무제한.
        /// </summary>
        public static string Offer(OfferStatus status, int stockLeft, string lockReason, bool stockFromTomorrow)
        {
            string stock = stockLeft >= 0 ? $"오늘 {stockLeft}개 남음" : string.Empty;
            switch (status)
            {
                case OfferStatus.Locked: return string.IsNullOrEmpty(lockReason) ? "잠김" : $"잠김{Separator}{lockReason}";
                case OfferStatus.SoldOut: return stockFromTomorrow ? "내일부터 입고" : "오늘 품절";
                case OfferStatus.MaxedOut: return "최대 단계";
                case OfferStatus.NotEnoughMoney: return Join(stock, "달러 부족");
                default: return stock;
            }
        }

        /// <summary>인벤토리 보유 수 ("보유 3개"). 0 이하면 빈 문자열.</summary>
        public static string Owned(int count) => count > 0 ? $"보유 {count}개" : string.Empty;

        /// <summary>스킬 단계 표시 "현재/최대".</summary>
        public static string SkillLevel(int level, int maxLevel) => $"{level}/{maxLevel}";

        /// <summary>
        /// 기획 문서에서 온 글을 게임 글꼴이 그릴 수 있게: 긴 줄표 · 빼기 기호("—" "–" "−")는 "-"로 (메이플스토리 글꼴에 없음, TASKS 트러블슈팅).
        /// </summary>
        public static string ForScreenFont(string text) =>
            string.IsNullOrEmpty(text) ? text : text.Replace('—', '-').Replace('–', '-').Replace('−', '-');

        /// <summary>조각을 " · "로 잇되 비어 있는 조각은 뺀다.</summary>
        public static string Join(params string[] parts)
        {
            var kept = new List<string>(parts.Length);
            foreach (var p in parts)
                if (!string.IsNullOrEmpty(p))
                    kept.Add(p);
            return string.Join(Separator, kept);
        }

        /// <summary>
        /// 이름들을 " · "로 잇되, 모두 같은 끝말(마지막 띄어쓰기 뒤)이면 한 번만 쓴다 — 레벨업 해금 줄이 길어지지 않게 (2026-10-29).
        /// 예: "괭이 업그레이드", "대걸레 업그레이드" → "괭이 · 대걸레 업그레이드". 하나뿐이거나 끝말이 다르면 그대로 잇는다.
        /// </summary>
        public static string JoinSharedTail(IReadOnlyList<string> names)
        {
            if (names == null || names.Count == 0)
                return string.Empty;
            if (names.Count == 1)
                return names[0];
            string tail = null;
            var heads = new List<string>(names.Count);
            foreach (var n in names)
            {
                int space = n != null ? n.LastIndexOf(' ') : -1;
                if (space <= 0 || space == n.Length - 1)
                    return string.Join(Separator, names);
                string t = n.Substring(space + 1);
                if (tail == null)
                    tail = t;
                else if (t != tail)
                    return string.Join(Separator, names);
                heads.Add(n.Substring(0, space));
            }
            return string.Join(Separator, heads) + " " + tail;
        }
    }
}
