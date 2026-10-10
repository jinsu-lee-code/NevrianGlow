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
