using System.Collections.Generic;
using NevrianGlowFarm.Logic.Data;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Tutorial;
using NUnit.Framework;

namespace NevrianGlowFarm.Tests
{
    /// <summary>튜토리얼 진행기 (TASKS 5-7): 조건 해석, 기획 CSV 순서대로 완료, 하루 누적, 잠금, 날 건너뛰기, 저장 · 복원.</summary>
    public class TutorialProgressTests
    {
        // 기획 CSV/plan/data_csv/tutorial_steps.csv (2026-10-22) 그대로 — 문구만 줄임
        private const string Csv = @"step_id,day,phase,text,highlight,complete_event,locks,mvp
D1-01,1,shop,(편지 ①),letter_ui,letter_closed:letter_1,move,P0
D1-02,1,shop,씨앗 4개,shop_tab_seed;shop_row:item_seed_moonroot,owned:item_seed_moonroot:4,buy_other;close_shop,P0
D1-03,1,farming,걷기,,moved:3,end_farming,P0
D1-04,1,farming,괭이,hotbar:1,selected:tool_hoe_1,end_farming,P0
D1-05,1,farming,한 칸,bed_tutorial:cell_0,tilled:1,end_farming,P0
D1-06,1,farming,나머지 3칸,bed_tutorial,tilled:4,end_farming,P0
D1-07,1,farming,심기,hotbar:4;bed_tutorial,planted:4,end_farming,P0
D1-08,1,farming,물,hotbar:2;bed_tutorial,watered:4,end_farming,P0
D1-09,1,farming,창고,storage_farm;storage_row:crop_moonroot,owned:crop_moonroot:2,end_farming,P0
D1-10,1,farming,가게로,btn_end_farming;door_shop,zone:zone_shop,,P0
D1-11,1,tidy,쓰레기 3개,trash_nearest:3;trash_bin,trashed:3,open_sales,P0
D1-12,1,tidy,원하는 만큼,hud_tidy_grade,auto,,P0
D1-13,1,tidy,진열 2개,fruit_shelf_1,displayed:2,open_sales,P0
D1-14,1,tidy,장사 시작,btn_open_sales;signboard,phase:sales,,P0
D1-15,1,sales,(피피),customer_pipi,sales_done,,P0
D1-16,1,settlement,결과,settle_revenue;settle_grade;settle_xp,next_day,,P0
D2-01,2,shop,(편지 ②),letter_ui,letter_closed:letter_2,move,P0
D2-02,2,shop,씨앗 넉넉히,shop_row:item_seed_moonroot,owned:item_seed_moonroot:4,close_shop,P0
D2-03,2,farming,수확,bed_tutorial,harvested:4,end_farming,P0
D2-04,2,farming,다시 심기,bed_tutorial;bed_next,planted:4,end_farming,P0
D2-05,2,farming,물,planted_cells,watered:4,end_farming,P0
D2-06,2,farming,가게로,btn_end_farming;door_shop,zone:zone_shop,,P0
D2-07,2,tidy,대걸레,stains;hotbar:3,stains_cleared:2,open_sales,P0
D2-08,2,tidy,진열 4개,fruit_shelf_1,displayed:4,open_sales,P0
D2-09,2,tidy,장사 시작,btn_open_sales;signboard,phase:sales,,P0
D2-10,2,sales,""(손님 3명, 피피 포함)"",customers,sales_done,,P0
D2-11,2,settlement,레벨업!,settle_xp;levelup_banner,leveled_up,,P0
D2-12,2,settlement,버섯꽃 해금,skill_node:skill_unlock_mushroom;btn_learn,skill:skill_unlock_mushroom,close_skill;other_skills,P0
D2-13,2,settlement,(편지 ③),letter_ui,letter_closed:letter_3,,P0
D2-14,2,settlement,다음 날,btn_next_day,next_day,,P0
X2-99,2,settlement,2차,,auto,,phase2
";

        private static List<TutorialStep> Steps() => TutorialStep.FromCsv(CsvTable.Parse(Csv));

        private static TutorialProgress Started()
        {
            var p = new TutorialProgress(Steps());
            p.Start(1);
            return p;
        }

        [Test]
        public void Condition_Parse()
        {
            var owned = TutorialCondition.Parse("owned:item_seed_moonroot:4");
            Assert.AreEqual(("owned", "item_seed_moonroot", 4f), (owned.Kind, owned.Target, owned.Threshold));
            var moved = TutorialCondition.Parse("moved:3");
            Assert.AreEqual(("moved", (string)null, 3f), (moved.Kind, moved.Target, moved.Threshold));
            var letter = TutorialCondition.Parse("letter_closed:letter_1");
            Assert.AreEqual(("letter_closed", "letter_1", 1f), (letter.Kind, letter.Target, letter.Threshold));
            var done = TutorialCondition.Parse("sales_done");
            Assert.AreEqual(("sales_done", (string)null, 1f), (done.Kind, done.Target, done.Threshold));
            Assert.IsTrue(TutorialCondition.Parse("auto").IsAuto);
            Assert.AreEqual("owned:item_seed_moonroot", owned.Key);
            Assert.AreEqual("moved", moved.Key);
        }

        [Test]
        public void FromCsv_ReadsRowsInOrder_SkipsPhase2()
        {
            var steps = Steps();
            Assert.AreEqual(30, steps.Count, "D1-01 ~ D2-14, phase2 행은 뺌");
            Assert.AreEqual("D1-01", steps[0].Id);
            Assert.AreEqual("D2-14", steps[29].Id);
            Assert.AreEqual(2, steps[16].Day);
            Assert.AreEqual("shop", steps[16].Phase);
            CollectionAssert.AreEqual(new[] { "buy_other", "close_shop" }, steps[1].Locks);
            Assert.AreEqual("(손님 3명, 피피 포함)", steps[25].Text, "따옴표 안 쉼표");
        }

        [Test]
        public void PhaseNames_MatchPlanCsv()
        {
            Assert.AreEqual("shop", TutorialPhaseNames.Of(DayPhase.Shop));
            Assert.AreEqual("farming", TutorialPhaseNames.Of(DayPhase.Farming));
            Assert.AreEqual("tidy", TutorialPhaseNames.Of(DayPhase.CleanDisplay));
            Assert.AreEqual("sales", TutorialPhaseNames.Of(DayPhase.Sales));
            Assert.AreEqual("settlement", TutorialPhaseNames.Of(DayPhase.Settlement));
        }

        [Test]
        public void Day1_FlowInOrder()
        {
            var p = Started();
            var seen = new List<string>();
            p.StepChanged += s => seen.Add(s?.Id ?? "-");
            Assert.AreEqual("D1-01", p.Current.Id);

            Assert.IsTrue(p.Report(TutorialEvents.LetterClosed, "letter_1"));
            Assert.AreEqual("D1-02", p.Current.Id);
            // 가진 개수는 누적이 아니라 지금 값
            p.SetState(TutorialEvents.Owned, "item_seed_moonroot", 3);
            Assert.AreEqual("D1-02", p.Current.Id);
            p.SetState(TutorialEvents.Owned, "item_seed_moonroot", 4);
            Assert.AreEqual("D1-03", p.Current.Id);
            // 걸은 거리는 조금씩 쌓인다
            p.Report(TutorialEvents.Moved, null, 1.5f);
            Assert.AreEqual("D1-03", p.Current.Id);
            Assert.AreEqual(1.5f, p.CurrentProgress, 1e-5f);
            p.Report(TutorialEvents.Moved, null, 1.5f);
            Assert.AreEqual("D1-04", p.Current.Id);
            p.Report(TutorialEvents.Selected, "tool_watering_1");
            Assert.AreEqual("D1-04", p.Current.Id, "다른 도구는 아님");
            p.Report(TutorialEvents.Selected, "tool_hoe_1");
            Assert.AreEqual("D1-05", p.Current.Id);
            // tilled 1 → 4는 하루 누적 (나머지 3칸)
            p.Report(TutorialEvents.Tilled);
            Assert.AreEqual("D1-06", p.Current.Id);
            p.Report(TutorialEvents.Tilled);
            p.Report(TutorialEvents.Tilled);
            Assert.AreEqual("D1-06", p.Current.Id);
            p.Report(TutorialEvents.Tilled);
            Assert.AreEqual("D1-07", p.Current.Id);
            for (int i = 0; i < 4; i++) p.Report(TutorialEvents.Planted, "crop_moonroot");
            Assert.AreEqual("D1-08", p.Current.Id);
            p.Report(TutorialEvents.Watered, null, 4);
            Assert.AreEqual("D1-09", p.Current.Id);
            p.SetState(TutorialEvents.Owned, "crop_moonroot", 2);
            Assert.AreEqual("D1-10", p.Current.Id);
            p.Report(TutorialEvents.Zone, "zone_farm");
            Assert.AreEqual("D1-10", p.Current.Id);
            p.Report(TutorialEvents.Zone, "zone_shop");
            Assert.AreEqual("D1-11", p.Current.Id);
            p.Report(TutorialEvents.Trashed, null, 3);
            Assert.AreEqual("D1-13", p.Current.Id, "D1-12(auto)는 바로 넘어감");
            CollectionAssert.Contains(seen, "D1-12");
            p.Report(TutorialEvents.Displayed, "crop_moonroot");
            p.Report(TutorialEvents.Displayed, "crop_moonroot");
            Assert.AreEqual("D1-14", p.Current.Id);
            p.Report(TutorialEvents.Phase, TutorialPhaseNames.Of(DayPhase.Sales));
            Assert.AreEqual("D1-15", p.Current.Id);
            p.Report(TutorialEvents.SalesDone);
            Assert.AreEqual("D1-16", p.Current.Id);
            p.Report(TutorialEvents.Phase, "settlement");
            Assert.AreEqual("D1-16", p.Current.Id);
            p.BeginDay(2); // = next_day
            Assert.AreEqual("D2-01", p.Current.Id);
            Assert.AreEqual(0f, p.CountOf(TutorialEvents.Tilled), "하루 누적은 날이 바뀌면 비움");
        }

        [Test]
        public void BeginDay_DoesNotCarryYesterdayCountsIntoNextDaySteps()
        {
            // 날마다 같은 조건(가진 개수 2 → 다음 날)이 이어질 때, 어제 양으로 다음 날 단계가 줄줄이 넘어가면 안 된다
            var steps = new List<TutorialStep>();
            for (int day = 1; day <= 3; day++)
            {
                steps.Add(new TutorialStep($"D{day}-01", day, "shop", "", "", "owned:item_seed_moonroot:2", ""));
                steps.Add(new TutorialStep($"D{day}-02", day, "settlement", "", "", "next_day", ""));
            }
            var p = new TutorialProgress(steps);
            p.Start(1);
            p.SetState(TutorialEvents.Owned, "item_seed_moonroot", 12);
            Assert.AreEqual("D1-02", p.Current.Id);
            p.BeginDay(2);
            Assert.AreEqual("D2-01", p.Current.Id);
            Assert.IsFalse(p.Report(TutorialEvents.NextDay), "따로 보낸 next_day는 지금 단계(가진 개수)와 무관");
            p.BeginDay(3);
            Assert.AreEqual("D3-01", p.Current.Id, "조건을 못 채운 2일차 단계는 건너뜀");
        }

        [Test]
        public void Day2_LevelUpBeforeSalesDone_StillCounts()
        {
            var p = new TutorialProgress(Steps());
            p.Restore("D2-09", 2);
            Assert.AreEqual("D2-09", p.Current.Id);
            p.Report(TutorialEvents.Phase, "sales");
            Assert.AreEqual("D2-10", p.Current.Id);
            // 게임은 장사 끝 처리 안에서 경험치 → 레벨업 알림을 먼저 보내고 장사 끝을 알린다
            p.Report(TutorialEvents.LeveledUp, null, 1);
            Assert.AreEqual("D2-10", p.Current.Id);
            p.Report(TutorialEvents.SalesDone);
            Assert.AreEqual("D2-12", p.Current.Id, "D2-11(레벨업)은 같은 날 이미 쌓인 양으로 바로 완료");
            Assert.IsTrue(p.IsLocked(TutorialLocks.CloseSkill));
            Assert.IsTrue(p.IsLockedFor(TutorialLocks.OtherSkills, "skill_fertile"));
            Assert.IsFalse(p.IsLockedFor(TutorialLocks.OtherSkills, "skill_unlock_mushroom"));
            p.Report(TutorialEvents.Skill, "skill_fertile");
            Assert.AreEqual("D2-12", p.Current.Id);
            p.Report(TutorialEvents.Skill, "skill_unlock_mushroom");
            Assert.AreEqual("D2-13", p.Current.Id);
            Assert.IsFalse(p.IsLocked(TutorialLocks.CloseSkill));
            p.Report(TutorialEvents.LetterClosed, "letter_3");
            Assert.AreEqual("D2-14", p.Current.Id);
            bool finished = false;
            p.Finished += () => finished = true;
            p.BeginDay(3);
            Assert.IsTrue(finished);
            Assert.AreEqual(TutorialStatus.Done, p.Status);
            Assert.IsNull(p.Current);
            Assert.IsFalse(p.Report(TutorialEvents.Tilled), "끝난 뒤에는 아무것도 안 함");
        }

        [Test]
        public void Locks_FollowCurrentStep()
        {
            var p = Started();
            Assert.IsTrue(p.IsLocked(TutorialLocks.Move));
            Assert.IsFalse(p.IsLocked(TutorialLocks.EndFarming));
            p.Report(TutorialEvents.LetterClosed, "letter_1");
            Assert.IsTrue(p.IsLocked(TutorialLocks.CloseShop));
            Assert.IsTrue(p.IsLockedFor(TutorialLocks.BuyOther, "shop_hoe_upgrade"));
            Assert.IsTrue(p.IsLockedFor(TutorialLocks.BuyOther, null));
            Assert.IsFalse(p.IsLockedFor(TutorialLocks.BuyOther, "item_seed_moonroot"));
            p.SetState(TutorialEvents.Owned, "item_seed_moonroot", 4);
            Assert.IsFalse(p.IsLocked(TutorialLocks.CloseShop));
            Assert.IsTrue(p.IsLocked(TutorialLocks.EndFarming));
            var off = new TutorialProgress(Steps());
            Assert.IsFalse(off.IsLocked(TutorialLocks.EndFarming), "안 켜졌으면 잠금 없음");
        }

        [Test]
        public void OutOfOrderEvents_AreIgnoredButCountedForTheDay()
        {
            var p = Started();
            p.Report(TutorialEvents.Tilled, null, 4);
            p.Report(TutorialEvents.SalesDone);
            Assert.AreEqual("D1-01", p.Current.Id, "편지를 닫기 전에는 안 넘어감");
            // 같은 날 쌓인 양은 그 단계가 되면 바로 잡힌다 (tilled 4 → D1-05 · D1-06을 한 번에)
            p.Report(TutorialEvents.LetterClosed, "letter_1");
            p.SetState(TutorialEvents.Owned, "item_seed_moonroot", 4);
            p.Report(TutorialEvents.Moved, null, 3);
            p.Report(TutorialEvents.Selected, "tool_hoe_1");
            Assert.AreEqual("D1-07", p.Current.Id);
        }

        [Test]
        public void BeginDay_SkipsEarlierSteps_AndFinishesPastLastDay()
        {
            var p = Started();
            p.Report(TutorialEvents.LetterClosed, "letter_1");
            p.SetState(TutorialEvents.Owned, "item_seed_moonroot", 4);
            Assert.AreEqual("D1-03", p.Current.Id);
            p.BeginDay(2);
            Assert.AreEqual("D2-01", p.Current.Id, "1일차 단계는 건너뜀");
            p.BeginDay(3);
            Assert.AreEqual(TutorialStatus.Done, p.Status);

            var late = new TutorialProgress(Steps());
            late.Start(3);
            Assert.AreEqual(TutorialStatus.Done, late.Status, "3일차에 시작하면 할 단계가 없음");
        }

        [Test]
        public void SaveKey_AndRestore()
        {
            var p = Started();
            p.Report(TutorialEvents.LetterClosed, "letter_1");
            Assert.AreEqual("D1-02", p.SaveKey);

            var back = new TutorialProgress(Steps());
            Assert.IsTrue(back.Restore(p.SaveKey, 1));
            Assert.AreEqual("D1-02", back.Current.Id);
            Assert.IsTrue(back.IsLocked(TutorialLocks.CloseShop));

            var done = new TutorialProgress(Steps());
            Assert.IsTrue(done.Restore(TutorialProgress.DoneKey, 2));
            Assert.AreEqual(TutorialStatus.Done, done.Status);
            Assert.AreEqual(TutorialProgress.DoneKey, done.SaveKey);

            var off = new TutorialProgress(Steps());
            Assert.IsTrue(off.Restore(null, 2));
            Assert.AreEqual(TutorialStatus.Off, off.Status);
            Assert.IsNull(off.SaveKey);

            var unknown = new TutorialProgress(Steps());
            Assert.IsFalse(unknown.Restore("D9-99", 2), "모르는 단계 id = 끝낸 것으로");
            Assert.AreEqual(TutorialStatus.Done, unknown.Status);

            // 1일차 단계를 2일차 저장에서 되돌리면 그날 단계로 맞춘다
            var mismatch = new TutorialProgress(Steps());
            Assert.IsTrue(mismatch.Restore("D1-05", 2));
            Assert.AreEqual("D2-01", mismatch.Current.Id);
        }

        [Test]
        public void RequiredFor_ReadsTodaysStepsOnly()
        {
            var p = Started();
            Assert.AreEqual(3f, p.RequiredFor(TutorialEvents.Trashed, 1));
            Assert.AreEqual(0f, p.RequiredFor(TutorialEvents.StainsCleared, 1), "1일차에는 얼룩 단계가 없음");
            Assert.AreEqual(2f, p.RequiredFor(TutorialEvents.StainsCleared, 2));
            Assert.AreEqual(1f, p.RequiredFor(TutorialEvents.LeveledUp, 2));
            Assert.AreEqual(0f, p.RequiredFor(TutorialEvents.LeveledUp, 1));
            p.TurnOff();
            Assert.AreEqual(0f, p.RequiredFor(TutorialEvents.StainsCleared, 2), "꺼지면 요구 없음");
        }

        [Test]
        public void EmptySteps_FinishImmediately()
        {
            var p = new TutorialProgress(new TutorialStep[0]);
            bool finished = false;
            p.Finished += () => finished = true;
            p.Start(1);
            Assert.IsTrue(finished);
            Assert.IsNull(p.Current);
        }
    }
}
