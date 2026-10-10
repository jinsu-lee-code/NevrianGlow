using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Shop
{
    /// <summary>해금 조건 종류 (조건 규칙이지 콘텐츠 종류가 아님). enum은 끝에만 추가.</summary>
    public enum UnlockKind
    {
        /// <summary>조건 없음.</summary>
        Always,
        /// <summary>스킬 TargetId를 Value단계 이상 배움.</summary>
        SkillLevel,
        /// <summary>Value일차 이상.</summary>
        DayAtLeast,
        /// <summary>작물 TargetId를 한 번이라도 수확함 (BALANCE 3-8 추천값 "버섯꽃 첫 수확").</summary>
        FirstHarvest,
    }

    public readonly struct UnlockRequirement
    {
        public UnlockKind Kind { get; }
        public string TargetId { get; }
        public int Value { get; }

        public UnlockRequirement(UnlockKind kind, string targetId = null, int value = 0)
        {
            Kind = kind;
            TargetId = targetId;
            Value = value;
        }
    }

    /// <summary>해금 조건이 묻는 게임 상태.</summary>
    public interface IUnlockContext
    {
        int Day { get; }
        int SkillLevel(string skillId);
        bool HasHarvested(string cropId);
    }

    public static class UnlockRules
    {
        public static bool IsMet(UnlockRequirement requirement, IUnlockContext context)
        {
            switch (requirement.Kind)
            {
                case UnlockKind.Always: return true;
                case UnlockKind.SkillLevel: return context.SkillLevel(requirement.TargetId) >= Math.Max(1, requirement.Value);
                case UnlockKind.DayAtLeast: return context.Day >= requirement.Value;
                case UnlockKind.FirstHarvest: return context.HasHarvested(requirement.TargetId);
                default: return false;
            }
        }

        /// <summary>모두 만족해야 열린다 (조건이 없으면 열림).</summary>
        public static bool AllMet(IReadOnlyList<UnlockRequirement> requirements, IUnlockContext context)
        {
            if (requirements == null)
                return true;
            foreach (var r in requirements)
                if (!IsMet(r, context))
                    return false;
            return true;
        }
    }

    /// <summary>상품 단계 하나: 가격 + 해금 조건.</summary>
    public readonly struct TierSpec
    {
        public int Price { get; }
        public IReadOnlyList<UnlockRequirement> Requirements { get; }

        public TierSpec(int price, IReadOnlyList<UnlockRequirement> requirements = null)
        {
            Price = Math.Max(0, price);
            Requirements = requirements ?? Array.Empty<UnlockRequirement>();
        }
    }

    /// <summary>
    /// 상품 규칙 (BALANCE 9-2). 반복 상품(씨앗 · 소모품) = 단계 1개를 하루 입고량만큼 여러 번,
    /// 단계형(도구 등급 · 밭 구획 · 가방 · 진열대) = 단계를 차례로 한 번씩.
    /// 하루 입고량: 일차별 표(1일차부터, 마지막 값이 이후 계속 — BALANCE 3-8 "일차별 입고량"), 비우면 무제한.
    /// StockFromNextDay = 처음 열린 날은 입고 없음, 다음 날부터 (BALANCE 3-8 "해금 다음 날부터 입고").
    /// </summary>
    public readonly struct ProductSpec
    {
        public string Id { get; }
        public IReadOnlyList<TierSpec> Tiers { get; }
        public bool Repeatable { get; }
        public IReadOnlyList<int> DailyStock { get; }
        public bool StockFromNextDay { get; }

        public ProductSpec(string id, IReadOnlyList<TierSpec> tiers, bool repeatable, IReadOnlyList<int> dailyStock = null, bool stockFromNextDay = false)
        {
            Id = id;
            Tiers = tiers ?? Array.Empty<TierSpec>();
            Repeatable = repeatable;
            DailyStock = dailyStock ?? Array.Empty<int>();
            StockFromNextDay = stockFromNextDay;
        }
    }

    /// <summary>상점에서 지금 살 수 있는지와 이유. enum은 끝에만 추가.</summary>
    public enum OfferStatus
    {
        Available,
        /// <summary>해금 조건을 못 채움.</summary>
        Locked,
        /// <summary>오늘 입고량을 다 삼 (또는 내일부터 입고).</summary>
        SoldOut,
        /// <summary>단계형을 끝까지 삼.</summary>
        MaxedOut,
        NotEnoughMoney,
    }

    public readonly struct ProductOffer
    {
        /// <summary>상점 목록에 보이는지. 해금 안 된 상품은 숨는다(GDD 4-6 "스킬로 해금해야 상점에 나타난다") — 단, 단계형에서 이미 앞 단계를 산 상품은 "다음 단계 잠김"으로 보인다.</summary>
        public bool Visible { get; }
        public OfferStatus Status { get; }
        /// <summary>이번에 사는 단계 (0부터). 반복 상품은 0.</summary>
        public int Tier { get; }
        public int Price { get; }
        /// <summary>오늘 남은 입고 (−1 = 무제한).</summary>
        public int StockLeft { get; }

        public ProductOffer(bool visible, OfferStatus status, int tier, int price, int stockLeft)
        {
            Visible = visible;
            Status = status;
            Tier = tier;
            Price = price;
            StockLeft = stockLeft;
        }
    }

    /// <summary>
    /// 상점 상태 (노션 GDD 12.4 ShopSystem): 단계형 상품을 몇 단계 샀는지 · 오늘 산 개수 · 처음 열린 날 · 오늘 쓴 돈. 엔진 의존 없음.
    /// 일차가 바뀔 때 StartDay(밤 처리 마지막 "상점 입고 초기화", GDD 4-2). 효과(아이템 지급 · 칸 늘리기)는 어댑터(ShopSystem)가 한다.
    /// </summary>
    public sealed class ShopState
    {
        private readonly Dictionary<string, int> tiersBought = new Dictionary<string, int>();
        private readonly Dictionary<string, int> boughtToday = new Dictionary<string, int>();
        private readonly Dictionary<string, int> firstOpenDay = new Dictionary<string, int>();

        /// <summary>오늘 상점에서 쓴 달러 (정산 화면 '지출').</summary>
        public int SpentToday { get; private set; }

        public int TiersBought(string productId) => productId != null && tiersBought.TryGetValue(productId, out int n) ? n : 0;
        public int BoughtToday(string productId) => productId != null && boughtToday.TryGetValue(productId, out int n) ? n : 0;

        /// <summary>처음 열린 일차 (아직 안 열렸으면 0).</summary>
        public int FirstOpenDay(string productId) => productId != null && firstOpenDay.TryGetValue(productId, out int d) ? d : 0;

        /// <summary>새 날: 오늘 산 개수 · 지출을 비운다 (입고 초기화).</summary>
        public void StartDay()
        {
            boughtToday.Clear();
            SpentToday = 0;
        }

        /// <summary>지금 열려 있으면 처음 열린 날을 기록한다 (일차 시작 · 스킬이 바뀔 때 어댑터가 부른다).</summary>
        public void NoteOpen(ProductSpec product, IUnlockContext context)
        {
            if (product.Id == null || firstOpenDay.ContainsKey(product.Id) || product.Tiers.Count == 0)
                return;
            int tier = product.Repeatable ? 0 : Math.Min(TiersBought(product.Id), product.Tiers.Count - 1);
            if (UnlockRules.AllMet(product.Tiers[tier].Requirements, context))
                firstOpenDay[product.Id] = context.Day;
        }

        /// <summary>오늘 입고량 (−1 = 무제한). 처음 열린 날 입고 없음 규칙 포함.</summary>
        public int StockToday(ProductSpec product, int day)
        {
            if (product.DailyStock.Count == 0)
                return -1;
            if (product.StockFromNextDay)
            {
                int open = FirstOpenDay(product.Id);
                if (open == 0 || open >= day)
                    return 0;
            }
            int index = Math.Min(Math.Max(0, day - 1), product.DailyStock.Count - 1);
            return Math.Max(0, product.DailyStock[index]);
        }

        public ProductOffer Offer(ProductSpec product, IUnlockContext context, int money)
        {
            if (product.Tiers.Count == 0)
                return new ProductOffer(false, OfferStatus.Locked, 0, 0, 0);
            int bought = TiersBought(product.Id);
            int tier = product.Repeatable ? 0 : bought;
            if (!product.Repeatable && tier >= product.Tiers.Count)
                return new ProductOffer(true, OfferStatus.MaxedOut, tier, 0, 0);

            var spec = product.Tiers[tier];
            int stock = StockToday(product, context.Day);
            int left = stock < 0 ? -1 : Math.Max(0, stock - BoughtToday(product.Id));
            if (!UnlockRules.AllMet(spec.Requirements, context))
                return new ProductOffer(!product.Repeatable && tier > 0, OfferStatus.Locked, tier, spec.Price, left);
            if (left == 0)
                return new ProductOffer(true, OfferStatus.SoldOut, tier, spec.Price, 0);
            if (money < spec.Price)
                return new ProductOffer(true, OfferStatus.NotEnoughMoney, tier, spec.Price, left);
            return new ProductOffer(true, OfferStatus.Available, tier, spec.Price, left);
        }

        /// <summary>살 수 있으면 기록하고 true (돈은 어댑터가 뺀다 — 같은 price). 효과를 적용할 수 없는지는 어댑터가 먼저 본다.</summary>
        public bool TryBuy(ProductSpec product, IUnlockContext context, int money, out ProductOffer offer)
        {
            offer = Offer(product, context, money);
            if (offer.Status != OfferStatus.Available)
                return false;
            boughtToday[product.Id] = BoughtToday(product.Id) + 1;
            if (!product.Repeatable)
                tiersBought[product.Id] = offer.Tier + 1;
            SpentToday += offer.Price;
            if (!firstOpenDay.ContainsKey(product.Id))
                firstOpenDay[product.Id] = context.Day;
            return true;
        }

        /// <summary>단계 수를 정한다 (불러오기 6-1 · 디버그).</summary>
        public void SetTiersBought(string productId, int tiers)
        {
            if (productId == null)
                return;
            if (tiers <= 0)
                tiersBought.Remove(productId);
            else
                tiersBought[productId] = tiers;
        }
    }
}
