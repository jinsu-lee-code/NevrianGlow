using System.Collections.Generic;
using NevrianGlowFarm.Logic.Data;
using NevrianGlowFarm.Logic.Farm;
using NevrianGlowFarm.Logic.Game;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>기획 CSV 가져오기 규칙 · 기획 확정으로 생긴 규칙 (2026-10-21): 대응표, 단계형 해금, 밭 칸 → 구획, 레벨 공식, 능력치 변환, 물 없는 작물, Set.</summary>
    public class PlanCsvTests
    {
        private const string Map = @"
| 종류 | 기획 id | 개발 id | 상태 | 변환 | 메모 |
|---|---|---|---|---|---|
| 씨앗 | seed_moonroot | item_seed_moonroot | 연결 | | |
| 장치 | device_sprinkler | - | 없음 | | 대기 |
| 상품 | - | shop_shelf_expand | 개발만 | | |
| 상점 분류 | consumable | 소모품 · 장치 | 연결 | | |
| 능력치 | sell_price_raw | `stat_sell_price` | 연결 | 1+값 배율 | |
| 스킬 | skill_signboard | skill_signboard | 숨김 | | |
";

        [Test]
        public void IdMap_ReadsTableAndDefaultsToSameId()
        {
            var map = IdMap.Parse(Map);
            Assert.AreEqual(6, map.Entries.Count);
            Assert.AreEqual("item_seed_moonroot", map.DevIdOf("seed_moonroot"));
            Assert.IsNull(map.DevIdOf("device_sprinkler"), "없음 = 개발 에셋 없음");
            Assert.AreEqual("crop_moonroot", map.DevIdOf("crop_moonroot"), "표에 없으면 같은 id");
            Assert.AreEqual("소모품 · 장치", map.DevIdOf("consumable", "상점 분류"));
            Assert.AreEqual("stat_sell_price", map.DevIdOf("sell_price_raw"), "백틱은 지운다");
            Assert.AreEqual(PlanCsv.BonusToMultiplier, map.Find("sell_price_raw").Conversion);
            Assert.AreEqual(IdMapStatus.Hidden, map.Find("skill_signboard").Status);
            CollectionAssert.AreEqual(new[] { "shop_shelf_expand" }, new List<IdMapEntry>(map.DevOnly()).ConvertAll(e => e.DevId));
        }

        [Test]
        public void Requirements_AndLists()
        {
            var reqs = PlanCsv.ParseRequirements("skill_unlock_grinder:1; skill_unlock_mushroom:2;skill_x");
            Assert.AreEqual(3, reqs.Count);
            Assert.AreEqual(("skill_unlock_mushroom", 2), reqs[1]);
            Assert.AreEqual(("skill_x", 1), reqs[2]);
            Assert.IsTrue(PlanCsv.TryIntList("1500;4000;10000;25000", out var prices));
            CollectionAssert.AreEqual(new[] { 1500, 4000, 10000, 25000 }, prices);
            Assert.IsFalse(PlanCsv.TryIntList("3;x", out _));
            CollectionAssert.AreEqual(new[] { "tool_hoe_t1", "tool_mop_t1" }, PlanCsv.SplitList("tool_hoe_t1;;tool_mop_t1;"));
        }

        [Test]
        public void TierLevels_BagLicense2_4_7()
        {
            // 가방 라이선스: 1단계 → 2단계까지, 2 → 4, 3 → 7 (skill_effects unlock_tier)
            var levels = PlanCsv.TierLevels(7, new[] { (2, 4), (1, 2), (3, 7) });
            CollectionAssert.AreEqual(new[] { 1, 1, 2, 2, 3, 3, 3 }, levels);
            // 물뿌리개 라이선스: 1 → 1, 2 → 2. 열리지 않는 단계는 -1
            CollectionAssert.AreEqual(new[] { 1, 2, -1 }, PlanCsv.TierLevels(3, new[] { (1, 1), (2, 2) }));
        }

        [Test]
        public void PlotsFromCells_AndLevelCurve()
        {
            Assert.IsTrue(PlanCsv.TryPlotsFromCells(112, 16, out int plots));
            Assert.AreEqual(7, plots);
            Assert.IsFalse(PlanCsv.TryPlotsFromCells(20, 16, out _));

            var (b, step) = PlanCsv.LevelCurveFromPlan(100, 50);
            var curve = new LevelCurve(1, b, step, 3);
            Assert.AreEqual(100, curve.RequiredFor(1), "회신 #3: 100 + 50 × (레벨 − 1)");
            Assert.AreEqual(150, curve.RequiredFor(2));
            Assert.AreEqual(300, curve.RequiredFor(5));
        }

        [Test]
        public void ConvertModifier_BonusBecomesMultiplier()
        {
            Assert.IsTrue(PlanCsv.TryConvertModifier("add", 0.05, PlanCsv.BonusToMultiplier, out var op, out double v));
            Assert.AreEqual(ModifierOp.Multiply, op);
            Assert.AreEqual(1.05, v, 1e-9);
            Assert.IsTrue(PlanCsv.TryConvertModifier("set", 5, "", out op, out v));
            Assert.AreEqual(ModifierOp.Set, op);
            Assert.AreEqual(5, v);
            Assert.IsTrue(PlanCsv.TryConvertModifier("mul", 0.9, "", out op, out _));
            Assert.AreEqual(ModifierOp.Multiply, op);
            Assert.IsFalse(PlanCsv.TryConvertModifier("pow", 2, "", out _, out _));
        }

        [Test]
        public void StatSet_ReplacesBaseThenAddAndMultiply()
        {
            var mods = new List<StatModifier>
            {
                new StatModifier("hotbar", ModifierOp.Set, 5),
                new StatModifier("hotbar", ModifierOp.Set, 6),
                new StatModifier("hotbar", ModifierOp.Add, 1),
            };
            Assert.AreEqual(7, StatCalculator.ApplyRounded("hotbar", 4, mods), "가장 큰 Set(6) + 1");
            Assert.AreEqual(4, StatCalculator.ApplyRounded("other", 4, mods));
        }

        [Test]
        public void CropWithoutWater_GrowsDryAndCannotBeWatered()
        {
            // 버섯꽃: 2일 · 물 불필요 (crops.csv needs_water = false, 회신 #30)
            var spec = new CropSpec(2, 1, 2, 0, 1, needsWater: false);
            var cell = new FarmCell();
            FarmRules.TryTill(cell, 3f, 3f);
            FarmRules.TryPlant(cell, "crop_mushroomflower");
            Assert.IsFalse(FarmRules.NeedsWatering(cell, spec));
            Assert.IsFalse(FarmRules.TryWater(cell, spec));
            FarmRules.GrowOneNight(cell, spec);
            FarmRules.GrowOneNight(cell, spec);
            Assert.IsTrue(FarmRules.IsReady(cell, spec));

            var watered = new CropSpec(1, 1, 1, 0, 1);
            var dry = new FarmCell();
            FarmRules.TryTill(dry, 3f, 3f);
            FarmRules.TryPlant(dry, "crop_moonroot");
            Assert.IsTrue(FarmRules.NeedsWatering(dry, watered));
            FarmRules.GrowOneNight(dry, watered);
            Assert.IsFalse(FarmRules.IsReady(dry, watered), "물이 필요한 작물은 물 없이 자라지 않는다 (그대로)");
        }
    }
}
