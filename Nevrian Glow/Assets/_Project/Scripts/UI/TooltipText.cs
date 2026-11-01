using System.Collections.Generic;
using System.Globalization;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Shop;
using NevrianGlowFarm.Logic.Text;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 데이터 에셋 → 툴팁 내용 (5-4c). 설명(기획 CSV description)이 있으면 그것 + 수치로 만든 한 줄, 없으면 수치 줄만.
    /// 글자 조립 규칙은 Logic UiText, 여기서는 어느 에셋 종류가 어떤 수치를 보여 줄지만 고른다 — 종류는 에셋 타입으로 구분(특정 id 분기 없음).
    /// 게임 화면 글자라 "—"를 쓰지 않는다.
    /// </summary>
    public static class TooltipText
    {
        /// <summary>인벤토리 · 창고 아이템. count는 보유 개수(0이면 적지 않음).</summary>
        public static TooltipContent ForItem(ItemData item, int count)
        {
            if (item == null)
                return default;
            string footer = count > 0 && item.MaxStack > 1 ? UiText.Owned(count) : null;
            return new TooltipContent(item.DisplayName, KindOf(item), Body(item.Description, Facts(item)), footer);
        }

        /// <summary>상점 상품 한 단계. owned = 주는 아이템의 보유 수(없으면 0), canBuy = 지금 단계에서 살 수 있는지.</summary>
        public static TooltipContent ForProduct(ShopProductData product, ProductOffer offer, string lockReason, bool stockFromTomorrow, int owned, bool canBuy)
        {
            if (product == null)
                return default;
            string facts = ProductFacts(product, offer.Tier);
            string price = offer.Status == OfferStatus.MaxedOut ? null : $"가격 {offer.Price:N0}달러";
            string footer = UiText.Join(price, UiText.Offer(offer.Status, offer.StockLeft, lockReason, stockFromTomorrow), UiText.Owned(owned),
                canBuy ? null : "지금은 보기만");
            return new TooltipContent(product.TierLabel(offer.Tier), product.Category, Body(product.Description, facts), footer);
        }

        /// <summary>스킬 한 줄: 단계별 효과 목록에 배운 단계 · 다음 단계를 표시.</summary>
        public static TooltipContent ForSkill(SkillData skill, int level, LearnCheck check, int nextCost)
        {
            if (skill == null)
                return default;
            var lines = new List<string>();
            for (int l = 1; l <= skill.MaxLevel; l++)
            {
                string mark = l <= level ? " (배움)" : l == level + 1 ? " (다음)" : string.Empty;
                lines.Add($"{l}단계: {EffectText(skill, l)}{mark}");
            }
            string body = Body(skill.Description, string.Join("\n", lines));
            string footer;
            switch (check)
            {
                case LearnCheck.MaxLevel: footer = "다 배움"; break;
                case LearnCheck.Prerequisite:
                    footer = UiText.Join($"다음 단계 {nextCost} SP",
                        skill.Prerequisite != null ? $"먼저 {skill.Prerequisite.DisplayName} {skill.PrerequisiteLevel}단계 필요" : "선행 스킬 필요");
                    break;
                case LearnCheck.NotEnoughPoints: footer = UiText.Join($"다음 단계 {nextCost} SP", "SP 부족"); break;
                default: footer = $"다음 단계 {nextCost} SP"; break;
            }
            return new TooltipContent($"{skill.DisplayName}  {UiText.SkillLevel(level, skill.MaxLevel)}단계", skill.Tab, body, footer);
        }

        /// <summary>단계 효과 글자 — 단계 설명이 있으면 그대로, 없으면 능력치 변경 값으로 (예: 판매가 ×1.1).</summary>
        public static string EffectText(SkillData skill, int level)
        {
            string described = skill.LevelDescription(level);
            if (described != null)
                return described;
            var parts = new List<string>();
            var mods = new List<StatModifier>();
            skill.CollectModifiers(level, mods);
            foreach (var m in mods)
            {
                string name = m.StatId;
                foreach (var e in skill.Modifiers)
                    if (e.Stat != null && e.Stat.Id == m.StatId)
                        name = e.Stat.DisplayName;
                string value = m.Value.ToString("0.##", CultureInfo.InvariantCulture);
                parts.Add(m.Op == ModifierOp.Add ? $"{name} {(m.Value >= 0 ? "+" : string.Empty)}{value}"
                    : m.Op == ModifierOp.Set ? $"{name} {value}" : $"{name} ×{value}");
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "효과 없음";
        }

        // 설명 + 수치 줄 (둘 다 있으면 두 줄, 하나만 있으면 그것만, 둘 다 없으면 null)
        private static string Body(string description, string facts)
        {
            bool d = !string.IsNullOrEmpty(description), f = !string.IsNullOrEmpty(facts);
            return d && f ? $"{description}\n{facts}" : d ? description : f ? facts : null;
        }

        // 아이템 종류 이름 (툴팁 작은 글자) — 에셋 타입으로
        private static string KindOf(ItemData item)
        {
            switch (item)
            {
                case SeedData _: return "씨앗";
                case CropData _: return "수확물";
                case ToolData _: return "도구";
                case FertilizerData _: return "비료";
                case FurnitureData _: return "가구";
                default: return "아이템";
            }
        }

        // 아이템 수치 한 줄 — 종류별로 보여 줄 값이 다르다
        private static string Facts(ItemData item)
        {
            switch (item)
            {
                case SeedData seed:
                    return seed.Crop != null ? UiText.Join($"심으면 {seed.Crop.DisplayName}", UiText.Crop(seed.Crop.ToSpec(), seed.Crop.SellPrice)) : null;
                case CropData crop:
                    return UiText.Crop(crop.ToSpec(), crop.SellPrice);
                case HoeData hoe:
                    return $"밭 칸을 간다 (클릭 한 번 작업량 {hoe.WorkPerUse.ToString("0.##", CultureInfo.InvariantCulture)})";
                case WateringCanData can:
                    return $"한 번에 {can.CellCount}칸에 물을 준다";
                case MopData mop:
                    return $"가게 바닥 얼룩을 닦는다 (반경 {mop.Radius.ToString("0.#", CultureInfo.InvariantCulture)} m)";
                case FertilizerData fertilizer:
                    return $"심은 작물이 주는 즉시 {fertilizer.GrowDaysReduction}일 자란다 (작물당 1회)";
                default:
                    return null;
            }
        }

        // 상품이 주는 것의 수치 — 아이템 상품은 그 아이템, 확장은 늘어나는 칸 수
        private static string ProductFacts(ShopProductData product, int tier)
        {
            switch (product)
            {
                case ItemProductData ip:
                    return ip.Item != null ? UiText.Join(ip.Count > 1 ? $"한 번에 {ip.Count}개" : null, Facts(ip.Item)) : null;
                case ToolUpgradeProductData tp:
                    return tp.ToolAt(tier) != null ? Facts(tp.ToolAt(tier)) : null;
                case BagExpandProductData bag:
                    return $"가방 칸 +{bag.Slots}";
                case PlotExpandProductData plot:
                    return $"밭 구획 +{plot.PlotsAt(tier)}";
                case ShelfExpandProductData shelf:
                    return $"진열 칸 +{shelf.Slots}";
                default:
                    return null;
            }
        }
    }
}
