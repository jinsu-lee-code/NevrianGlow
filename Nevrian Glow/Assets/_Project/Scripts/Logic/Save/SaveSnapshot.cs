using System.Collections.Generic;
using NevrianGlowFarm.Logic.Farm;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Inventory;
using NevrianGlowFarm.Logic.Shop;

namespace NevrianGlowFarm.Logic.Save
{
    /// <summary>밭 한 칸의 저장 값 (FarmCell 그대로).</summary>
    public struct CellSave
    {
        public float TillProgress;
        public bool IsTilled;
        public string CropId;
        public int Age;
        public int WetDaysLeft;
        public int HarvestCount;
        public int FertilizerBonus;
    }

    /// <summary>바닥 아이템 한 묶음 (GDD 4-8: id · 개수 · 구역 · 위치 · 회전).</summary>
    public struct WorldItemSave
    {
        public string ItemId;
        public int Count;
        /// <summary>놓인 구역 씬 이름 (Farm · Shop).</summary>
        public string Zone;
        public float X, Y, Z;
        public float Yaw;
    }

    /// <summary>
    /// 한 판의 저장 내용 (GDD 4-8, TASKS 6-1). 콘텐츠는 모두 id로. 엔진 의존 없음 — 글로 쓰고 읽기는 SaveFormat,
    /// Logic 상태와 오가는 일은 SaveState, 씬의 시스템과 오가는 일은 SaveSystem(어댑터).
    /// </summary>
    public sealed class SaveSnapshot
    {
        public int Day { get; set; } = DayCycle.FirstDay;
        public DayPhase Phase { get; set; } = DayPhase.Shop;

        public int Money { get; set; }
        public int SkillPoints { get; set; }
        public int Level { get; set; } = Progression.FirstLevel;
        public int Exp { get; set; }

        public Dictionary<string, int> Skills { get; } = new Dictionary<string, int>();
        public List<string> Harvested { get; } = new List<string>();
        public List<SettlementReport> Settlements { get; } = new List<SettlementReport>();

        public int PlotCount { get; set; }
        public List<CellSave> Cells { get; } = new List<CellSave>();

        public List<ItemStack> Hotbar { get; } = new List<ItemStack>();
        public List<ItemStack> Bag { get; } = new List<ItemStack>();
        public List<ItemStack> Storage { get; } = new List<ItemStack>();
        /// <summary>진열대마다 칸 목록 (진열대 순서 = 씬에서 찾는 순서).</summary>
        public List<List<ItemStack>> Shelves { get; } = new List<List<ItemStack>>();

        public Dictionary<string, int> ShopTiers { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> ShopBoughtToday { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> ShopFirstOpen { get; } = new Dictionary<string, int>();
        public int ShopSpentToday { get; set; }

        public List<WorldItemSave> WorldItems { get; } = new List<WorldItemSave>();
    }

    /// <summary>
    /// Logic 상태 ↔ 저장 내용. 게임(어댑터) · 테스트가 같은 코드를 쓴다 (CLAUDE 규칙과 표현 분리).
    /// </summary>
    public static class SaveState
    {
        // ── 밭 ──

        public static void CaptureFarm(FarmState farm, SaveSnapshot into)
        {
            into.PlotCount = farm.PlotCount;
            into.Cells.Clear();
            foreach (var c in farm.Cells)
                into.Cells.Add(new CellSave
                {
                    TillProgress = c.TillProgress,
                    IsTilled = c.IsTilled,
                    CropId = c.CropId,
                    Age = c.Age,
                    WetDaysLeft = c.WetDaysLeft,
                    HarvestCount = c.HarvestCount,
                    FertilizerBonus = c.FertilizerBonus,
                });
        }

        /// <summary>구획 수를 맞추고(늘리기만) 칸 값을 덮어쓴다. 저장보다 칸이 많으면 남는 칸은 비운다.</summary>
        public static void ApplyFarm(FarmState farm, SaveSnapshot from)
        {
            if (from.PlotCount > farm.PlotCount)
                farm.SetPlotCount(from.PlotCount);
            for (int i = 0; i < farm.Cells.Count; i++)
            {
                var cell = farm.Cells[i];
                var s = i < from.Cells.Count ? from.Cells[i] : default;
                cell.TillProgress = s.TillProgress;
                cell.IsTilled = s.IsTilled;
                cell.CropId = string.IsNullOrEmpty(s.CropId) ? null : s.CropId;
                cell.Age = s.Age;
                cell.WetDaysLeft = s.WetDaysLeft;
                cell.HarvestCount = s.HarvestCount;
                cell.FertilizerBonus = s.FertilizerBonus;
            }
        }

        // ── 칸 묶음 · 창고 · 진열대 ──

        public static void Capture(SlotInventory inventory, List<ItemStack> into)
        {
            into.Clear();
            for (int i = 0; i < inventory.SlotCount; i++)
                into.Add(inventory[i]);
        }

        public static void Capture(Storage storage, List<ItemStack> into)
        {
            into.Clear();
            into.AddRange(storage.Entries);
        }

        public static void Apply(Storage storage, IReadOnlyList<ItemStack> from)
        {
            storage.Clear();
            foreach (var s in from)
                if (!s.IsEmpty)
                    storage.TryDeposit(s.ItemId, s.Count);
        }

        public static List<ItemStack> Capture(ShelfState shelf)
        {
            var list = new List<ItemStack>(shelf.SlotCount);
            for (int i = 0; i < shelf.SlotCount; i++)
                list.Add(shelf[i]);
            return list;
        }

        // ── 상점 ──

        public static void CaptureShop(ShopState shop, SaveSnapshot into)
        {
            Copy(shop.AllTiersBought, into.ShopTiers);
            Copy(shop.AllBoughtToday, into.ShopBoughtToday);
            Copy(shop.AllFirstOpenDays, into.ShopFirstOpen);
            into.ShopSpentToday = shop.SpentToday;
        }

        public static void ApplyShop(ShopState shop, SaveSnapshot from) =>
            shop.Load(from.ShopTiers, from.ShopBoughtToday, from.ShopFirstOpen, from.ShopSpentToday);

        // ── 진행 ──

        public static void CaptureProgress(SkillLevels skills, GameRecord record, RunSummary run, SaveSnapshot into)
        {
            Copy(skills.All, into.Skills);
            into.Harvested.Clear();
            into.Harvested.AddRange(record.Harvested);
            into.Harvested.Sort(System.StringComparer.Ordinal);
            into.Settlements.Clear();
            into.Settlements.AddRange(run.Days);
        }

        public static void ApplyProgress(SkillLevels skills, GameRecord record, RunSummary run, SaveSnapshot from)
        {
            skills.Clear();
            foreach (var pair in from.Skills)
                skills.SetLevel(pair.Key, pair.Value);
            record.Clear();
            foreach (var id in from.Harvested)
                record.RecordHarvest(id);
            run.Clear();
            foreach (var r in from.Settlements)
                run.Record(r);
        }

        // ── 없어진 콘텐츠 (GDD 4-8: 없어진 id는 건너뛰고 로그) ──

        /// <summary>
        /// known(id)가 거짓인 아이템 · 작물 · 스킬을 지운다. 지운 내용을 log에 한 줄씩. 지운 개수를 돌려준다.
        /// 작물이 없어진 칸은 빈 미개간 칸, 아이템 칸은 빈 칸, 바닥 아이템 · 창고 줄은 뺀다.
        /// </summary>
        public static int RemoveUnknown(SaveSnapshot s, System.Func<string, bool> known, List<string> log)
        {
            int removed = 0;
            for (int i = 0; i < s.Cells.Count; i++)
            {
                var c = s.Cells[i];
                if (string.IsNullOrEmpty(c.CropId) || known(c.CropId))
                    continue;
                log?.Add($"밭 {i}번 칸의 작물 '{c.CropId}' 없음 - 빈 칸으로");
                s.Cells[i] = default;
                removed++;
            }
            removed += Clean(s.Hotbar, known, log, "핫바");
            removed += Clean(s.Bag, known, log, "가방");
            for (int k = 0; k < s.Shelves.Count; k++)
                removed += Clean(s.Shelves[k], known, log, $"진열대 {k}");
            for (int i = s.Storage.Count - 1; i >= 0; i--)
                if (!known(s.Storage[i].ItemId))
                {
                    log?.Add($"창고의 '{s.Storage[i].ItemId}' ×{s.Storage[i].Count} 없음 - 뺌");
                    s.Storage.RemoveAt(i);
                    removed++;
                }
            for (int i = s.WorldItems.Count - 1; i >= 0; i--)
                if (!known(s.WorldItems[i].ItemId))
                {
                    log?.Add($"바닥 아이템 '{s.WorldItems[i].ItemId}' 없음 - 뺌");
                    s.WorldItems.RemoveAt(i);
                    removed++;
                }
            foreach (var id in new List<string>(s.Skills.Keys))
                if (!known(id))
                {
                    log?.Add($"스킬 '{id}' 없음 - 뺌");
                    s.Skills.Remove(id);
                    removed++;
                }
            return removed;
        }

        private static int Clean(List<ItemStack> stacks, System.Func<string, bool> known, List<string> log, string where)
        {
            int removed = 0;
            for (int i = 0; i < stacks.Count; i++)
                if (!stacks[i].IsEmpty && !known(stacks[i].ItemId))
                {
                    log?.Add($"{where} {i}번 칸의 '{stacks[i].ItemId}' ×{stacks[i].Count} 없음 - 빈 칸으로");
                    stacks[i] = ItemStack.Empty;
                    removed++;
                }
            return removed;
        }

        private static void Copy(IReadOnlyDictionary<string, int> from, Dictionary<string, int> to)
        {
            to.Clear();
            foreach (var pair in from)
                to[pair.Key] = pair.Value;
        }
    }
}
