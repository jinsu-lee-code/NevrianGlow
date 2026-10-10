using System;
using System.Collections.Generic;
using System.Linq;
using NevrianGlowFarm.Logic.Farm;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Sales;
using NevrianGlowFarm.Logic.Shop;

namespace NevrianGlowFarm.Logic.Simulation
{
    /// <summary>봇 하루 결과 (리포트 한 줄, BALANCE 4번 6).</summary>
    public sealed class SimDay
    {
        public int Day { get; set; }
        public int Customers { get; set; }
        public int ItemsSold { get; set; }
        public int Revenue { get; set; }
        public int Spent { get; set; }
        /// <summary>그날 끝(정산 · 스킬 뒤) 보유 달러.</summary>
        public int Money { get; set; }
        public int Level { get; set; }
        /// <summary>그날 끝 남은 SP (스킬에 쓰고 남은 것).</summary>
        public int SkillPoints { get; set; }
        /// <summary>그날 배운 스킬 (id 단계).</summary>
        public List<string> SkillsLearned { get; } = new List<string>();
        /// <summary>열린 밭 칸 수.</summary>
        public int OpenCells { get; set; }
        /// <summary>그날 밤 작물이 있는 칸 수.</summary>
        public int PlantedCells { get; set; }
        /// <summary>작물 id → 그날 새로 심은 칸.</summary>
        public Dictionary<string, int> Planted { get; } = new Dictionary<string, int>();
        /// <summary>작물 id → 그날 수확한 개수.</summary>
        public Dictionary<string, int> Harvested { get; } = new Dictionary<string, int>();
        /// <summary>그날 상점에 씨앗이 보인 작물.</summary>
        public HashSet<string> Unlocked { get; } = new HashSet<string>();
        /// <summary>장사 뒤 팔리지 않은 수확물 (진열대 + 창고).</summary>
        public int Unsold { get; set; }
        /// <summary>그날 판정한 목표 (Goals 순서, 그날 목표만).</summary>
        public List<KeyValuePair<SimGoal, bool>> Goals { get; } = new List<KeyValuePair<SimGoal, bool>>();

        public int PlantedTotal => Planted.Values.Sum();
        public int HarvestedTotal => Harvested.Values.Sum();
        public bool AllGoalsMet => Goals.All(g => g.Value);
    }

    /// <summary>봇 한 판 (시드 하나).</summary>
    public sealed class SimRun
    {
        public int Seed { get; set; }
        public List<SimDay> Days { get; } = new List<SimDay>();
        public int GoalDaysMet => Days.Count(d => d.Goals.Count > 0 && d.AllGoalsMet);
    }

    /// <summary>
    /// 측정 봇 (TASKS 4-1, BALANCE 4번 "적당히 잘하는 플레이어"). 게임과 같은 Logic 규칙(밭 · 상점 · 장사 · 성장)으로 하루를 돈다.
    /// 하루: 상점(구획 → 칸당 이익 순 씨앗) → 농사(수확 → 개간 · 심기 → 물) → 청소(청결도 고정) → 진열(전부) → 장사(건너뛰기와 같은 함수)
    /// → 정산(경험치 · SP) → 스킬(우선순위) → 밤 처리 → 상점 입고 초기화.
    /// 시간 · 이동 · 인벤토리 칸 수는 보지 않는다(봇은 창고를 오가며 다 처리한다고 가정).
    /// </summary>
    public static class DaySimulator
    {
        private sealed class Context : IUnlockContext
        {
            public int Day { get; set; }
            public SkillLevels Skills { get; } = new SkillLevels();
            public GameRecord Record { get; } = new GameRecord();
            public int SkillLevel(string skillId) => Skills.LevelOf(skillId);
            public bool HasHarvested(string cropId) => Record.HasHarvested(cropId);
        }

        public static SimRun Run(DaySimConfig config, int seed)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var crops = config.Crops.ToDictionary(c => c.Id);
            // 칸당 이익이 큰 작물부터 (같으면 id 순 — 같은 데이터면 같은 결과)
            var byProfit = config.Crops.OrderByDescending(c => c.ProfitPerCellPerDay).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();

            var farm = new FarmState(config.CellsPerPlot, config.StartPlots, id => crops[id].Spec);
            var night = new NightPipeline();
            var shop = new ShopState();
            var shelf = new ShelfState(config.ShelfSlots, config.ShelfItemsPerSlot);
            var shelves = new List<ShelfState> { shelf };
            var wallet = new Wallet(config.StartMoney, 0);
            var progression = new Progression(config.Curve);
            var ctx = new Context();
            var seeds = new Dictionary<string, int>(config.StartSeeds);
            var storage = new Dictionary<string, int>(config.StartCrops);
            var run = new SimRun { Seed = seed };
            Func<string, int> priceOf = id => crops.TryGetValue(id, out var c) ? c.SellPrice : 0;

            for (int day = 1; day <= config.Days; day++)
            {
                ctx.Day = day;
                shop.StartDay();
                var row = new SimDay { Day = day };

                // ── 상점 ──
                foreach (var c in config.Crops)
                    if (c.HasSeedProduct)
                    {
                        shop.NoteOpen(c.SeedProduct, ctx);
                        if (shop.Offer(c.SeedProduct, ctx, wallet.Money).Visible)
                            row.Unlocked.Add(c.Id);
                    }
                // 구획 가격 이상 남으면 구획을 먼저 (하루 한 단계) — 실험 옵션이면 씨앗 뒤 남은 돈으로
                if (config.PlotBeforeSeeds)
                    TryBuyPlot(config, shop, ctx, wallet, farm);
                int need = FreeCellsToday(farm, crops) - seeds.Values.Sum();
                foreach (var c in byProfit)
                {
                    if (!c.HasSeedProduct)
                        continue;
                    while (need > 0 && shop.TryBuy(c.SeedProduct, ctx, wallet.Money, out var offer))
                    {
                        wallet.TryChangeMoney(-offer.Price);
                        Add(seeds, c.Id, c.SeedsPerBuy);
                        need -= c.SeedsPerBuy;
                    }
                }
                if (!config.PlotBeforeSeeds)
                    TryBuyPlot(config, shop, ctx, wallet, farm);
                row.Spent = shop.SpentToday;

                // ── 농사: 수확 → 개간 · 심기(이익 큰 씨앗부터) → 물 ──
                foreach (var cell in farm.Cells)
                {
                    if (!cell.HasCrop)
                        continue;
                    var h = FarmRules.TryHarvest(cell, crops[cell.CropId].Spec);
                    if (!h.Succeeded)
                        continue;
                    Add(storage, h.CropId, h.Yield);
                    Add(row.Harvested, h.CropId, h.Yield);
                    ctx.Record.RecordHarvest(h.CropId);
                }
                foreach (var c in byProfit)
                {
                    foreach (var cell in farm.Cells)
                    {
                        if (!seeds.TryGetValue(c.Id, out int n) || n <= 0)
                            break;
                        if (cell.HasCrop)
                            continue;
                        FarmRules.TryTill(cell, 1f, 0f); // 봇은 필요한 만큼 갈았다고 본다 (작업량 0 = 한 번에 개간됨)
                        if (FarmRules.TryPlant(cell, c.Id))
                        {
                            seeds[c.Id] = n - 1;
                            Add(row.Planted, c.Id, 1);
                        }
                    }
                }
                foreach (var cell in farm.Cells)
                    if (cell.HasCrop)
                        FarmRules.TryWater(cell, crops[cell.CropId].Spec);

                // ── 진열: 창고 수확물을 진열대가 허용하는 만큼 (비싼 작물부터) ──
                foreach (var c in config.Crops.OrderByDescending(c => c.SellPrice).ThenBy(c => c.Id, StringComparer.Ordinal))
                    for (int slot = 0; slot < shelf.SlotCount; slot++)
                        while (storage.TryGetValue(c.Id, out int n) && n > 0 && shelf.TryPlace(slot, c.Id))
                            storage[c.Id] = n - 1;

                // ── 장사 (청결도 고정 · 건너뛰기와 같은 함수) ──
                int customers = SalesRules.CustomerCount(day, config.CustomerBonus, config.Cleanliness, config.Sales);
                var session = new SalesSession(customers, config.Sales);
                var random = new SeededRandom(SeededRandom.SeedFor(seed, DaySimConfig.PurchasePurpose, day));
                session.SkipRemaining(shelves, random, priceOf);
                wallet.TryChangeMoney(session.Revenue);
                row.Customers = session.Total;
                row.ItemsSold = session.ItemsSold;
                row.Revenue = session.Revenue;
                row.Unsold = shelf.TotalCount + storage.Values.Sum();

                // ── 정산 → 스킬 ──
                var gain = progression.AddRevenue(session.Revenue);
                if (gain.SkillPointsGained > 0)
                    wallet.TryChangeSkillPoints(gain.SkillPointsGained);
                LearnSkills(config.SkillPriority, ctx.Skills, wallet, row.SkillsLearned);

                row.Money = wallet.Money;
                row.Level = progression.Level;
                row.SkillPoints = wallet.SkillPoints;
                row.OpenCells = farm.Cells.Count;
                row.PlantedCells = farm.Cells.Count(c => c.HasCrop);
                run.Days.Add(row);
                JudgeGoals(config.Goals, row, run.Days.Count > 1 ? run.Days[run.Days.Count - 2] : null);

                // ── 밤 ──
                night.Run(farm);
            }
            return run;
        }

        private static void TryBuyPlot(DaySimConfig config, ShopState shop, Context ctx, Wallet wallet, FarmState farm)
        {
            var plot = config.PlotProduct;
            if (plot.Id == null || plot.Tiers == null)
                return;
            if (shop.TryBuy(plot, ctx, wallet.Money, out var bought) && wallet.TryChangeMoney(-bought.Price))
                farm.SetPlotCount(farm.PlotCount + Math.Max(1, config.PlotsPerTier));
        }

        /// <summary>오늘 심을 수 있는 칸 =작물 없는 칸 + 오늘 수확하면 비는 칸 (봇은 다 자란 것을 안다).</summary>
        public static int FreeCellsToday(FarmState farm, IReadOnlyDictionary<string, SimCrop> crops)
        {
            int free = 0;
            foreach (var cell in farm.Cells)
            {
                if (!cell.HasCrop)
                {
                    free++;
                    continue;
                }
                var spec = crops[cell.CropId].Spec;
                bool lastHarvest = spec.RegrowDays <= 0 || cell.HarvestCount + 1 >= spec.MaxHarvests;
                if (FarmRules.IsReady(cell, spec) && lastHarvest)
                    free++;
            }
            return free;
        }

        /// <summary>우선순위대로 배운다. 앞 스킬의 다음 단계를 살 수 없으면 거기서 멈춘다(아껴 둠). 다 배운 스킬은 건너뛴다.</summary>
        private static void LearnSkills(IReadOnlyList<SimSkill> priority, SkillLevels skills, Wallet wallet, List<string> learned)
        {
            foreach (var skill in priority)
            {
                while (skills.LevelOf(skill.Id) < skill.Costs.Length)
                {
                    int level = skills.LevelOf(skill.Id);
                    bool prereq = string.IsNullOrEmpty(skill.PrerequisiteId) || skills.LevelOf(skill.PrerequisiteId) >= skill.PrerequisiteLevel;
                    if (!prereq || !wallet.TryChangeSkillPoints(-skill.Costs[level]))
                        return;
                    skills.SetLevel(skill.Id, level + 1);
                    learned.Add($"{skill.Id} {level + 1}");
                }
            }
        }

        private static void JudgeGoals(IReadOnlyList<SimGoal> goals, SimDay row, SimDay previous)
        {
            foreach (var goal in goals)
            {
                if (goal.Day != row.Day)
                    continue;
                bool met;
                switch (goal.Kind)
                {
                    case SimGoalKind.PlantedAtLeast: met = CountOf(row.Planted, goal.CropId) >= goal.Amount; break;
                    case SimGoalKind.HarvestedAtLeast: met = CountOf(row.Harvested, goal.CropId) >= goal.Amount; break;
                    case SimGoalKind.CropUnlocked: met = goal.CropId != null && row.Unlocked.Contains(goal.CropId); break;
                    case SimGoalKind.PlantedMoreThanPreviousDay:
                        met = CountOf(row.Planted, goal.CropId) > (previous != null ? CountOf(previous.Planted, goal.CropId) : 0);
                        break;
                    default: met = false; break;
                }
                row.Goals.Add(new KeyValuePair<SimGoal, bool>(goal, met));
            }
        }

        private static int CountOf(Dictionary<string, int> counts, string cropId) =>
            string.IsNullOrEmpty(cropId) ? counts.Values.Sum() : counts.TryGetValue(cropId, out int n) ? n : 0;

        private static void Add(Dictionary<string, int> counts, string id, int amount) =>
            counts[id] = counts.TryGetValue(id, out int n) ? n + amount : amount;
    }
}
