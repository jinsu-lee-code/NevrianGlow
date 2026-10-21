using System.Collections.Generic;
using NevrianGlowFarm.Logic.Farm;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Inventory;
using NevrianGlowFarm.Logic.Save;
using NevrianGlowFarm.Logic.Shop;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    public class SaveFormatTests
    {
        // BALANCE 2-2 작물 3종 (v1)
        private static CropSpec Spec(string id) =>
            id == "crop_moonroot" ? new CropSpec(1, 1, 1, 0, 1)
            : id == "crop_mushroomflower" ? new CropSpec(2, 2, 2, 0, 1)
            : new CropSpec(8, 1, 2, 4, 3);

        private static int MaxStack(string id) => id.StartsWith("tool_") ? 1 : 10;

        /// <summary>저장 대상 Logic 상태 묶음 — 게임의 시스템들이 들고 있는 것과 같은 종류.</summary>
        private sealed class Model
        {
            public DayCycle Cycle = new DayCycle();
            public Wallet Wallet = new Wallet(150, 0);
            public Progression Progress = new Progression(new LevelCurve(1, 40, 40, 3));
            public SkillLevels Skills = new SkillLevels();
            public GameRecord Record = new GameRecord();
            public RunSummary Run = new RunSummary();
            public FarmState Farm = new FarmState(4, 2, Spec);
            public NightPipeline Night = new NightPipeline();
            public SlotInventory Hotbar = new SlotInventory(4, MaxStack);
            public SlotInventory Bag = new SlotInventory(4, MaxStack);
            public Storage Storage = new Storage(id => id != "item_trash");
            public ShelfState Shelf = new ShelfState(6, 4);
            public ShopState Shop = new ShopState();

            public SaveSnapshot Capture()
            {
                var s = new SaveSnapshot
                {
                    Day = Cycle.Day, Phase = Cycle.Phase, Money = Wallet.Money, SkillPoints = Wallet.SkillPoints,
                    Level = Progress.Level, Exp = Progress.Exp,
                };
                SaveState.CaptureProgress(Skills, Record, Run, s);
                SaveState.CaptureFarm(Farm, s);
                SaveState.Capture(Hotbar, s.Hotbar);
                SaveState.Capture(Bag, s.Bag);
                SaveState.Capture(Storage, s.Storage);
                s.Shelves.Add(SaveState.Capture(Shelf));
                SaveState.CaptureShop(Shop, s);
                s.WorldItems.Add(new WorldItemSave { ItemId = "item_trash", Count = 1, Zone = "Shop", X = 500.25f, Y = 0.02f, Z = -3.5f, Yaw = 137.5f });
                return s;
            }

            public static Model From(SaveSnapshot s)
            {
                var m = new Model
                {
                    Cycle = new DayCycle(s.Day, s.Phase),
                    Wallet = new Wallet(s.Money, s.SkillPoints),
                    Progress = new Progression(new LevelCurve(1, 40, 40, 3), s.Level, s.Exp),
                };
                SaveState.ApplyProgress(m.Skills, m.Record, m.Run, s);
                SaveState.ApplyFarm(m.Farm, s);
                m.Hotbar.Load(s.Hotbar);
                m.Bag.Load(s.Bag);
                SaveState.Apply(m.Storage, s.Storage);
                m.Shelf.Load(s.Shelves[0]);
                SaveState.ApplyShop(m.Shop, s);
                return m;
            }

            /// <summary>하루를 정해진 순서로 보낸다 (난수 없음): 상점 → 농사(수확 · 갈기 · 심기 · 물) → 진열 → 장사(진열 1개씩 판매) → 정산 → 밤.</summary>
            public void PlayDay()
            {
                int day = Cycle.Day;
                // 상점: 월광 씨앗 2개
                var seed = new ProductSpec("shop_seed_moonroot", new[] { new TierSpec(10) }, true, new[] { 12 });
                for (int n = 0; n < 2; n++)
                    if (Shop.TryBuy(seed, new Ctx(this), Wallet.Money, out var offer))
                    {
                        Wallet.TryChangeMoney(-offer.Price);
                        InventoryRules.TryAddAll(new[] { Hotbar, Bag }, "item_seed_moonroot", 1);
                    }
                Cycle.Advance();

                // 농사: 다 자란 칸 수확 → 빈 칸 갈고 심고 물
                for (int i = 0; i < Farm.Cells.Count; i++)
                {
                    var c = Farm.Cells[i];
                    if (c.HasCrop)
                    {
                        var h = FarmRules.TryHarvest(c, Spec(c.CropId));
                        if (h.Succeeded)
                        {
                            InventoryRules.TryAddAll(new[] { Hotbar, Bag }, h.CropId, h.Yield);
                            Record.RecordHarvest(h.CropId);
                        }
                    }
                }
                int planted = 0;
                for (int i = 0; i < Farm.Cells.Count && planted < 2; i++)
                {
                    var c = Farm.Cells[i];
                    if (c.HasCrop || InventoryRules.CountOf(new[] { Hotbar, Bag }, "item_seed_moonroot") == 0)
                        continue;
                    FarmRules.TryTill(c, 3f, 3f);
                    if (FarmRules.TryPlant(c, i % 3 == 0 ? "crop_mushroomflower" : "crop_moonroot"))
                    {
                        RemoveOne(Hotbar, Bag, "item_seed_moonroot");
                        planted++;
                    }
                }
                foreach (var c in Farm.Cells)
                    if (c.HasCrop)
                        FarmRules.TryWater(c, Spec(c.CropId));
                Cycle.Advance();

                // 진열: 수확물을 첫 칸부터
                foreach (var id in new[] { "crop_moonroot", "crop_mushroomflower" })
                    for (int slot = 0; slot < Shelf.SlotCount; slot++)
                        while (InventoryRules.CountOf(new[] { Hotbar, Bag }, id) > 0 && Shelf.TryPlace(slot, id))
                            RemoveOne(Hotbar, Bag, id);
                Cycle.Advance();

                // 장사: 진열 칸마다 1개 팔림
                int revenue = 0, sold = 0;
                for (int slot = 0; slot < Shelf.SlotCount; slot++)
                {
                    string id = Shelf.TakeOne(slot);
                    if (id == null)
                        continue;
                    revenue += id == "crop_moonroot" ? 15 : 40;
                    sold++;
                }
                if (revenue > 0)
                    Wallet.TryChangeMoney(revenue);
                var gain = Progress.AddRevenue(revenue);
                if (gain.SkillPointsGained > 0)
                    Wallet.TryChangeSkillPoints(gain.SkillPointsGained);
                Cycle.Advance();

                // 정산 · 스킬: SP가 3 이상이면 버섯꽃 해금
                Run.Record(new SettlementReport(day, true, sold, sold, 0, sold, revenue, new Dictionary<string, int>(), Shop.SpentToday, gain, Progress.Level));
                if (Wallet.SkillPoints >= 3 && Skills.LevelOf("skill_unlock_mushroom") == 0)
                {
                    Wallet.TryChangeSkillPoints(-3);
                    Skills.SetLevel("skill_unlock_mushroom", 1);
                }
                Cycle.Advance();
                Cycle.Advance(); // 다음 날 아침

                // 밤 처리 + 상점 입고 초기화 (게임과 같은 순서)
                Night.Run(Farm);
                Shop.StartDay();
            }

            private static void RemoveOne(SlotInventory a, SlotInventory b, string id)
            {
                foreach (var inv in new[] { a, b })
                    for (int i = 0; i < inv.SlotCount; i++)
                        if (inv[i].ItemId == id && inv.TryRemoveAt(i, 1))
                            return;
            }

            private sealed class Ctx : IUnlockContext
            {
                private readonly Model m;
                public Ctx(Model m) => this.m = m;
                public int Day => m.Cycle.Day;
                public int SkillLevel(string skillId) => m.Skills.LevelOf(skillId);
                public bool HasHarvested(string cropId) => m.Record.HasHarvested(cropId);
            }
        }

        private static Model Started()
        {
            var m = new Model();
            m.Hotbar.TryPlace(0, "tool_hoe_1", 1);
            m.Hotbar.TryPlace(1, "tool_watering_1", 1);
            m.Hotbar.TryPlace(2, "item_seed_moonroot", 10);
            m.Storage.TryDeposit("crop_moonroot", 2);
            m.Storage.TryDeposit("tool_mop_1", 1);
            m.PlayDay(); // 1일차를 보내 밭 · 진열 · 상점 · 정산 기록이 차게
            return m;
        }

        [Test]
        public void WriteRead_SameText()
        {
            var s = Started().Capture();
            string text = SaveFormat.Write(s);

            Assert.IsTrue(SaveFormat.TryRead(text, out var back, out string error), error);
            Assert.AreEqual(text, SaveFormat.Write(back));
            Assert.AreEqual(2, back.Day);
            Assert.AreEqual(DayPhase.Shop, back.Phase);
            Assert.AreEqual(8, back.Cells.Count);
            Assert.AreEqual(137.5f, back.WorldItems[0].Yaw);
        }

        [Test]
        public void SaveThenLoad_ThenThreeDays_EqualsThreeDaysWithoutSaving()
        {
            // GDD 4-8 · CLAUDE 핵심 규칙 7: 저장 → 불러와 진행 = 저장 없이 진행
            var plain = Started();
            var loaded = Model.From(ReadBack(SaveFormat.Write(Started().Capture())));

            for (int i = 0; i < 3; i++)
            {
                plain.PlayDay();
                loaded.PlayDay();
            }
            Assert.AreEqual(SaveFormat.Write(plain.Capture()), SaveFormat.Write(loaded.Capture()));
            Assert.AreEqual(5, plain.Cycle.Day);
            Assert.Greater(plain.Run.DaysRecorded, 3, "4일치 정산이 쌓였다");
        }

        [Test]
        public void BrokenFiles_AreRejected()
        {
            string good = SaveFormat.Write(Started().Capture());
            Assert.IsFalse(SaveFormat.TryRead("", out _, out _), "빈 파일");
            Assert.IsFalse(SaveFormat.TryRead("hello 1\nend\n", out _, out _), "첫 줄이 다름");
            Assert.IsFalse(SaveFormat.TryRead(good.Replace("ngf_save 1", "ngf_save 99"), out _, out string newer), "더 새 버전");
            StringAssert.Contains("99", newer);
            Assert.IsFalse(SaveFormat.TryRead(good.Replace("end\n", string.Empty), out _, out string cut), "끝 표시 없음 = 쓰다 끊김");
            StringAssert.Contains("end", cut);
            Assert.IsFalse(SaveFormat.TryRead(good.Replace("money ", "money x"), out _, out _), "숫자 자리에 글자");
            Assert.IsFalse(SaveFormat.TryRead(good + "day 3\n", out _, out _), "end 뒤 내용");
        }

        [Test]
        public void UnknownKey_IsSkippedWithWarning()
        {
            string text = SaveFormat.Write(Started().Capture()).Replace("end\n", "future_thing 1 2\nend\n");
            var warnings = new List<string>();
            Assert.IsTrue(SaveFormat.TryRead(text, out _, out string error, warnings), error);
            Assert.AreEqual(1, warnings.Count);
        }

        [Test]
        public void RemovedContent_IsSkipped()
        {
            var s = Started().Capture();
            s.Storage.Add(new ItemStack("item_gone", 3));
            s.Hotbar[3] = new ItemStack("item_gone", 2);
            s.Skills["skill_gone"] = 1;
            s.Cells[0] = new CellSave { IsTilled = true, CropId = "crop_gone", Age = 1 };
            var log = new List<string>();

            int removed = SaveState.RemoveUnknown(s, id => !id.EndsWith("_gone"), log);

            Assert.AreEqual(4, removed);
            Assert.AreEqual(4, log.Count);
            Assert.IsTrue(s.Hotbar[3].IsEmpty);
            Assert.IsNull(s.Cells[0].CropId);
            Assert.IsFalse(s.Skills.ContainsKey("skill_gone"));
        }

        [Test]
        public void Load_ClipsStacksOverMax()
        {
            var inv = new SlotInventory(2, MaxStack);
            int clipped = inv.Load(new[] { new ItemStack("tool_hoe_1", 3), new ItemStack("crop_moonroot", 4) });
            Assert.AreEqual(2, clipped);
            Assert.AreEqual(1, inv[0].Count);
            Assert.AreEqual(4, inv[1].Count);
        }

        private static SaveSnapshot ReadBack(string text)
        {
            Assert.IsTrue(SaveFormat.TryRead(text, out var s, out string error), error);
            return s;
        }
    }
}
