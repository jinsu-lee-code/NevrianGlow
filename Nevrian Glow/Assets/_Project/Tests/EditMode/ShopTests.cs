using System.Collections.Generic;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Shop;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>3-3a 상점: 해금 조건 · 하루 입고량(일차별 표 · 다음 날부터) · 단계형 · 숨김 · 지출 · 입고 초기화.</summary>
    public class ShopTests
    {
        private sealed class Context : IUnlockContext
        {
            public int Day { get; set; } = 1;
            public int Level { get; set; } = 1;
            public readonly SkillLevels Skills = new SkillLevels();
            public readonly GameRecord Record = new GameRecord();
            public int SkillLevel(string skillId) => Skills.LevelOf(skillId);
            public bool HasHarvested(string cropId) => Record.HasHarvested(cropId);
        }

        private static UnlockRequirement Skill(string id, int level) => new UnlockRequirement(UnlockKind.SkillLevel, id, level);

        private static ProductSpec Seed(int price, int[] stock, UnlockRequirement[] reqs = null, bool nextDay = false) =>
            new ProductSpec("shop_seed", new[] { new TierSpec(price, reqs) }, true, stock, nextDay);

        private static ProductSpec Tiered(params TierSpec[] tiers) => new ProductSpec("shop_tiered", tiers, false);

        [Test]
        public void Unlock_KindsWork()
        {
            var ctx = new Context { Day = 3 };
            Assert.IsTrue(UnlockRules.IsMet(new UnlockRequirement(UnlockKind.Always), ctx));
            Assert.IsFalse(UnlockRules.IsMet(Skill("skill_a", 1), ctx));
            ctx.Skills.SetLevel("skill_a", 2);
            Assert.IsTrue(UnlockRules.IsMet(Skill("skill_a", 2), ctx));
            Assert.IsFalse(UnlockRules.IsMet(Skill("skill_a", 3), ctx));
            Assert.IsTrue(UnlockRules.IsMet(new UnlockRequirement(UnlockKind.DayAtLeast, null, 3), ctx));
            Assert.IsFalse(UnlockRules.IsMet(new UnlockRequirement(UnlockKind.DayAtLeast, null, 4), ctx));
            Assert.IsFalse(UnlockRules.IsMet(new UnlockRequirement(UnlockKind.FirstHarvest, "crop_x"), ctx));
            Assert.IsTrue(ctx.Record.RecordHarvest("crop_x"));
            Assert.IsFalse(ctx.Record.RecordHarvest("crop_x"), "두 번째는 처음 아님");
            Assert.IsTrue(UnlockRules.IsMet(new UnlockRequirement(UnlockKind.FirstHarvest, "crop_x"), ctx));
            Assert.IsTrue(UnlockRules.AllMet(new[] { Skill("skill_a", 1), new UnlockRequirement(UnlockKind.FirstHarvest, "crop_x") }, ctx));
            Assert.IsFalse(UnlockRules.AllMet(new[] { Skill("skill_a", 1), Skill("skill_b", 1) }, ctx), "모두 만족해야");
        }

        // 스킬트리 폐기 → 레벨업 해금 (수정사항 10/28): 레벨 2에서 씨앗, 단계형 도구는 모든 단계가 같은 레벨 조건
        [Test]
        public void LevelUnlock_OpensAtLevel_AllTiers()
        {
            var shop = new ShopState();
            var ctx = new Context { Day = 1, Level = 1 };
            var level2 = new UnlockRequirement(UnlockKind.LevelAtLeast, null, 2);
            var seed = Seed(15, new[] { 20 }, new[] { level2 }, nextDay: true);
            Assert.IsFalse(UnlockRules.IsMet(level2, ctx));
            Assert.IsFalse(shop.Offer(seed, ctx, 1000).Visible, "레벨 1에서는 숨김");

            ctx.Level = 2; // 1일차 정산에서 레벨업 → 그 순간 열린 날 기록
            shop.NoteOpen(seed, ctx);
            Assert.AreEqual(OfferStatus.SoldOut, shop.Offer(seed, ctx, 1000).Status, "레벨업한 날은 입고 없음");
            ctx.Day = 2;
            shop.StartDay();
            Assert.AreEqual(20, shop.Offer(seed, ctx, 1000).StockLeft, "다음 날 아침부터 입고");

            var level3 = new UnlockRequirement(UnlockKind.LevelAtLeast, null, 3);
            var hoe = Tiered(new TierSpec(150, new[] { level3 }), new TierSpec(1200, new[] { level3 }));
            Assert.AreEqual(OfferStatus.Locked, shop.Offer(hoe, ctx, 5000).Status);
            ctx.Level = 3;
            Assert.IsTrue(shop.TryBuy(hoe, ctx, 5000, out _));
            Assert.IsTrue(shop.TryBuy(hoe, ctx, 5000, out _), "레벨 3이면 2단계도");
        }

        // 수정사항 10/28: 레벨 6에서 씨앗 입고량 두 배 (해금이 아니라 그냥 제공)
        [Test]
        public void BonusStock_FromLevel()
        {
            var shop = new ShopState();
            var seed = new ProductSpec("shop_seed", new[] { new TierSpec(5) }, true, new[] { 40 }, false, bonusStock: 80, bonusStockLevel: 6);
            Assert.AreEqual(40, shop.StockToday(seed, 3, 5));
            Assert.AreEqual(80, shop.StockToday(seed, 3, 6));
            Assert.AreEqual(80, shop.StockToday(seed, 3, 9));
            Assert.AreEqual(40, shop.StockToday(seed, 3), "레벨을 모르면 기본");
            Assert.AreEqual(80, shop.Offer(seed, new Context { Day = 5, Level = 6 }, 1000).StockLeft);

            var none = new ProductSpec("shop_x", new[] { new TierSpec(5) }, true, new[] { 40 });
            Assert.AreEqual(40, shop.StockToday(none, 3, 10), "보너스가 없는 상품은 그대로");
        }

        // 기획 shop.csv 10/30: "하루 입고 12개, 레벨마다 +2 (최대 40)" · 비료 "하루 입고 6개(레벨 5), 레벨마다 +1 (최대 10)"
        [Test]
        public void StockPerLevel_FromUnlockLevel_Capped()
        {
            var shop = new ShopState();
            var moonroot = new ProductSpec("shop_seed_moonroot", new[] { new TierSpec(5) }, true, new[] { 12 }, stockPerLevel: 2, stockMax: 40);
            Assert.AreEqual(12, shop.StockToday(moonroot, 1, 1));
            Assert.AreEqual(14, shop.StockToday(moonroot, 2, 2));
            Assert.AreEqual(30, shop.StockToday(moonroot, 5, 10));
            Assert.AreEqual(40, shop.StockToday(moonroot, 6, 20), "최대 40");

            var level5 = new UnlockRequirement(UnlockKind.LevelAtLeast, null, 5);
            var fertilizer = new ProductSpec("shop_fertilizer", new[] { new TierSpec(40, new[] { level5 }) }, true, new[] { 6 }, stockPerLevel: 1, stockMax: 10);
            Assert.AreEqual(5, fertilizer.UnlockLevel);
            Assert.AreEqual(6, shop.StockToday(fertilizer, 4, 5), "해금 레벨에서 기본 입고");
            Assert.AreEqual(8, shop.StockToday(fertilizer, 5, 7));
            Assert.AreEqual(10, shop.StockToday(fertilizer, 6, 12));

            // 레벨마다 입고가 있으면 예전 레벨 보너스 입고는 쓰지 않는다
            var both = new ProductSpec("shop_x", new[] { new TierSpec(5) }, true, new[] { 12 }, false, 80, 6, 2, 40);
            Assert.AreEqual(22, shop.StockToday(both, 3, 6));
        }

        // 기획 shop.csv 10/30 새 상품: 정해진 5단계 뒤에는 "직전 가격 × 4"로 끝없이
        [Test]
        public void EndlessTiers_PriceGrowsAfterList()
        {
            var shop = new ShopState();
            var ctx = new Context();
            var basket = new ProductSpec("shop_big_basket", new[] { new TierSpec(200), new TierSpec(800), new TierSpec(3200) }, false, extraTierGrowth: 4.0);
            Assert.IsTrue(shop.TryBuy(basket, ctx, 1_000_000, out var o1));
            Assert.AreEqual(200, o1.Price);
            shop.TryBuy(basket, ctx, 1_000_000, out _);
            shop.TryBuy(basket, ctx, 1_000_000, out var o3);
            Assert.AreEqual(3200, o3.Price);
            var o4 = shop.Offer(basket, ctx, 1_000_000);
            Assert.AreEqual(OfferStatus.Available, o4.Status, "목록 뒤에도 계속");
            Assert.AreEqual(3, o4.Tier);
            Assert.AreEqual(12800, o4.Price);
            shop.TryBuy(basket, ctx, 1_000_000, out _);
            Assert.AreEqual(51200, shop.Offer(basket, ctx, 1_000_000).Price);
            Assert.AreEqual(4, shop.TiersBought("shop_big_basket"));

            var tool = Tiered(new TierSpec(150), new TierSpec(380));
            shop.TryBuy(tool, ctx, 10_000, out _);
            shop.TryBuy(tool, ctx, 10_000, out _);
            Assert.AreEqual(OfferStatus.MaxedOut, shop.Offer(tool, ctx, 10_000).Status, "배율이 없으면 마지막 단계에서 끝");
        }

        [Test]
        public void SkillLevels_SetAndClamp()
        {
            var skills = new SkillLevels();
            Assert.AreEqual(0, skills.LevelOf("skill_a"));
            Assert.IsTrue(skills.SetLevel("skill_a", 2));
            Assert.IsFalse(skills.SetLevel("skill_a", 2), "같으면 안 바뀜");
            Assert.IsTrue(skills.SetLevel("skill_a", -3));
            Assert.AreEqual(0, skills.LevelOf("skill_a"));
            Assert.IsFalse(skills.SetLevel(null, 1));
        }

        [Test]
        public void Repeatable_DailyStockAndRestock()
        {
            var shop = new ShopState();
            var ctx = new Context();
            var seed = Seed(10, new[] { 3 });
            for (int i = 0; i < 3; i++)
                Assert.IsTrue(shop.TryBuy(seed, ctx, 1000, out _));
            Assert.AreEqual(OfferStatus.SoldOut, shop.Offer(seed, ctx, 1000).Status);
            Assert.IsFalse(shop.TryBuy(seed, ctx, 1000, out _));
            Assert.AreEqual(30, shop.SpentToday);

            ctx.Day = 2;
            shop.StartDay();
            var offer = shop.Offer(seed, ctx, 1000);
            Assert.AreEqual(OfferStatus.Available, offer.Status);
            Assert.AreEqual(3, offer.StockLeft, "새 날 입고 초기화");
            Assert.AreEqual(0, shop.SpentToday);
        }

        [Test]
        public void DailyStock_PerDayTable_LastRepeats_EmptyUnlimited()
        {
            var shop = new ShopState();
            var seed = Seed(10, new[] { 4, 12 }); // BALANCE 3-8 추천값 형태: 1일차 4 → 이후 12
            Assert.AreEqual(4, shop.StockToday(seed, 1));
            Assert.AreEqual(12, shop.StockToday(seed, 2));
            Assert.AreEqual(12, shop.StockToday(seed, 6));
            Assert.AreEqual(-1, shop.StockToday(Seed(10, new int[0]), 1), "비우면 무제한");
            Assert.AreEqual(-1, shop.Offer(Seed(10, new int[0]), new Context(), 1000).StockLeft);
        }

        [Test]
        public void StockFromNextDay_NoStockOnOpenDay()
        {
            var shop = new ShopState();
            var ctx = new Context { Day = 3 };
            var seed = Seed(20, new[] { 8 }, new[] { Skill("skill_unlock", 1) }, nextDay: true);
            Assert.IsFalse(shop.Offer(seed, ctx, 1000).Visible, "해금 전 숨김");
            ctx.Skills.SetLevel("skill_unlock", 1);
            shop.NoteOpen(seed, ctx);
            Assert.AreEqual(3, shop.FirstOpenDay("shop_seed"));
            Assert.AreEqual(OfferStatus.SoldOut, shop.Offer(seed, ctx, 1000).Status, "열린 날은 입고 없음");
            ctx.Day = 4;
            shop.StartDay();
            shop.NoteOpen(seed, ctx);
            Assert.AreEqual(3, shop.FirstOpenDay("shop_seed"), "처음 열린 날은 그대로");
            Assert.AreEqual(8, shop.Offer(seed, ctx, 1000).StockLeft);
        }

        [Test]
        public void Tiered_InOrder_ThenMaxed()
        {
            var shop = new ShopState();
            var ctx = new Context();
            var hoe = Tiered(new TierSpec(150), new TierSpec(400));
            var o = shop.Offer(hoe, ctx, 1000);
            Assert.AreEqual(0, o.Tier);
            Assert.AreEqual(150, o.Price);
            Assert.AreEqual(-1, o.StockLeft);
            Assert.IsTrue(shop.TryBuy(hoe, ctx, 1000, out var bought));
            Assert.AreEqual(0, bought.Tier);
            Assert.AreEqual(1, shop.Offer(hoe, ctx, 1000).Tier);
            Assert.AreEqual(400, shop.Offer(hoe, ctx, 1000).Price);
            Assert.IsTrue(shop.TryBuy(hoe, ctx, 1000, out _), "단계형은 하루에 여러 단계 살 수 있음");
            var maxed = shop.Offer(hoe, ctx, 1000);
            Assert.AreEqual(OfferStatus.MaxedOut, maxed.Status);
            Assert.IsTrue(maxed.Visible);
            Assert.IsFalse(shop.TryBuy(hoe, ctx, 1000, out _));
            Assert.AreEqual(550, shop.SpentToday);
            Assert.AreEqual(2, shop.TiersBought("shop_tiered"));
        }

        [Test]
        public void Tiered_LockedTier_HiddenAtStart_VisibleAfterFirst()
        {
            // 물뿌리개: 단계마다 면허 단계 필요 (BALANCE 9-2)
            var shop = new ShopState();
            var ctx = new Context();
            var can = Tiered(new TierSpec(100, new[] { Skill("license", 1) }), new TierSpec(250, new[] { Skill("license", 2) }));
            var start = shop.Offer(can, ctx, 1000);
            Assert.AreEqual(OfferStatus.Locked, start.Status);
            Assert.IsFalse(start.Visible, "아직 아무 단계도 안 열리면 상점에 안 보임");
            ctx.Skills.SetLevel("license", 1);
            Assert.IsTrue(shop.TryBuy(can, ctx, 1000, out _));
            var next = shop.Offer(can, ctx, 1000);
            Assert.AreEqual(OfferStatus.Locked, next.Status);
            Assert.IsTrue(next.Visible, "앞 단계를 산 뒤에는 다음 단계 잠김으로 보임");
            ctx.Skills.SetLevel("license", 2);
            Assert.AreEqual(OfferStatus.Available, shop.Offer(can, ctx, 1000).Status);
        }

        [Test]
        public void NotEnoughMoney_NothingRecorded()
        {
            var shop = new ShopState();
            var ctx = new Context();
            var seed = Seed(70, new[] { 4 });
            Assert.AreEqual(OfferStatus.NotEnoughMoney, shop.Offer(seed, ctx, 69).Status);
            Assert.IsFalse(shop.TryBuy(seed, ctx, 69, out _));
            Assert.AreEqual(0, shop.BoughtToday("shop_seed"));
            Assert.AreEqual(0, shop.SpentToday);
            Assert.IsTrue(shop.TryBuy(seed, ctx, 70, out _), "딱 맞으면 삼");
        }
    }
}
