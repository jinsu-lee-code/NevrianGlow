using System.Collections.Generic;
using NevrianGlowFarm.Logic.Tutorial;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>튜토리얼 v3 오늘의 목표 조건 (3 ~ 6일차 체크리스트).</summary>
    public class GoalConditionsTests
    {
        private sealed class World : IGoalWorld
        {
            public readonly Dictionary<string, int> Tiers = new Dictionary<string, int>();
            public readonly HashSet<string> Planted = new HashSet<string>();
            public readonly HashSet<string> Fertilized = new HashSet<string>();
            public readonly HashSet<string> Harvested = new HashSet<string>();
            public int TiersBought(string productId) => Tiers.TryGetValue(productId, out int n) ? n : 0;
            public int PlantedCells { get; set; }
            public bool HasPlanted(string cropId) => Planted.Contains(cropId);
            public bool HasFertilized(string cropId) => Fertilized.Contains(cropId);
            public bool SitePlanted { get; set; }
            public bool HasHarvested(string cropId) => Harvested.Contains(cropId);
            public bool SalesDoneToday { get; set; }
        }

        [Test]
        public void Day3_IronHoe_FullField_Mushroom()
        {
            var w = new World();
            Assert.IsFalse(GoalConditions.IsMet("bought:shop_hoe_upgrade:1", w));
            w.Tiers["shop_hoe_upgrade"] = 1;
            Assert.IsTrue(GoalConditions.IsMet("bought:shop_hoe_upgrade:1", w));
            Assert.IsFalse(GoalConditions.IsMet("planted_total:32", w));
            w.PlantedCells = 32;
            Assert.IsTrue(GoalConditions.IsMet("planted_total:32", w));
            Assert.IsFalse(GoalConditions.IsMet("planted:crop_mushroomflower", w));
            w.Planted.Add("crop_mushroomflower");
            Assert.IsTrue(GoalConditions.IsMet("planted:crop_mushroomflower", w));
        }

        [Test]
        public void AllAnyAndOr()
        {
            var w = new World();
            w.Tiers["shop_mop_upgrade"] = 1;
            Assert.IsFalse(GoalConditions.IsMet("bought:shop_mop_upgrade,shop_watering_upgrade:1", w), "둘 다 사야");
            Assert.IsTrue(GoalConditions.IsMet("bought_any:shop_store_promo,shop_mop_upgrade:1", w), "하나라도");
            // 6일차: 2단계 업그레이드 하나 이상 또는 밭 확장 2
            const string day6 = "bought_any:shop_hoe_upgrade,shop_mop_upgrade,shop_watering_upgrade:2 || bought:shop_plot_expand:2";
            Assert.IsFalse(GoalConditions.IsMet(day6, w));
            w.Tiers["shop_plot_expand"] = 2;
            Assert.IsTrue(GoalConditions.IsMet(day6, w));
        }

        [Test]
        public void StateFlags_AndValidation()
        {
            var w = new World();
            Assert.IsFalse(GoalConditions.IsMet("sales_done", w));
            w.SalesDoneToday = true;
            w.SitePlanted = true;
            w.Harvested.Add("crop_lanternfruit");
            w.Fertilized.Add("crop_lanternfruit");
            Assert.IsTrue(GoalConditions.IsMet("sales_done", w));
            Assert.IsTrue(GoalConditions.IsMet("site_planted", w));
            Assert.IsTrue(GoalConditions.IsMet("harvested:crop_lanternfruit", w));
            Assert.IsTrue(GoalConditions.IsMet("fertilized:crop_lanternfruit", w));
            Assert.IsTrue(GoalConditions.IsValid("bought_any:a,b:2 || planted_total:64"));
            Assert.IsFalse(GoalConditions.IsValid("bought:a"), "단계 없음");
            Assert.IsFalse(GoalConditions.IsValid("unknown:x"));
            Assert.IsFalse(GoalConditions.IsMet("unknown:x", w), "모르는 조건은 거짓");
        }
    }
}
