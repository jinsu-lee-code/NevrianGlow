using System;
using System.Collections.Generic;
using System.Globalization;
using NevrianGlowFarm.Logic.Data;
using NevrianGlowFarm.Logic.Game;

namespace NevrianGlowFarm.Logic.Tutorial
{
    /// <summary>
    /// 완료 이벤트 이름 (기획 tutorial_steps.csv complete_event, 개발 검토 2026-10-22 5절). 게임은 이 이름으로만 알린다 — 특정 단계 id로 분기하지 않는다.
    /// 형식 "종류[:대상][:수]" — 대상은 콘텐츠 id · 지역 id · 단계 이름, 수는 하루 누적 개수(없으면 1).
    /// </summary>
    public static class TutorialEvents
    {
        /// <summary>바로 다음 단계.</summary>
        public const string Auto = "auto";
        /// <summary>편지 닫기 (대상 = 편지 id).</summary>
        public const string LetterClosed = "letter_closed";
        /// <summary>걸은 거리 (수 = m).</summary>
        public const string Moved = "moved";
        /// <summary>핫바 + 가방에 그 개수 이상 (대상 = 아이템 id, 수 = 개수) — 누적이 아니라 지금 개수.</summary>
        public const string Owned = "owned";
        /// <summary>핫바에서 그 아이템 들기 (대상 = 아이템 id).</summary>
        public const string Selected = "selected";
        public const string Tilled = "tilled";
        public const string Planted = "planted";
        public const string Watered = "watered";
        public const string Harvested = "harvested";
        /// <summary>그 지역 도착 (대상 = 지역 id).</summary>
        public const string Zone = "zone";
        public const string Trashed = "trashed";
        public const string StainsCleared = "stains_cleared";
        public const string Displayed = "displayed";
        /// <summary>단계 시작 (대상 = 단계 이름, TutorialPhaseNames).</summary>
        public const string Phase = "phase";
        public const string SalesDone = "sales_done";
        public const string NextDay = "next_day";
        public const string LeveledUp = "leveled_up";
        /// <summary>스킬 습득 (대상 = 스킬 id).</summary>
        public const string Skill = "skill";
    }

    /// <summary>잠금 이름 (기획 CSV locks). 게임의 각 입구가 자기 이름으로 묻는다 — "흐름을 깨는 것만" 잠근다.</summary>
    public static class TutorialLocks
    {
        /// <summary>농사 끝내기 (농장 문 · HUD 버튼).</summary>
        public const string EndFarming = "end_farming";
        /// <summary>상점 닫기 (B · Esc).</summary>
        public const string CloseShop = "close_shop";
        /// <summary>장사 시작 (간판 · HUD 버튼).</summary>
        public const string OpenSales = "open_sales";
        /// <summary>스킬 화면 닫기 (K · Esc).</summary>
        public const string CloseSkill = "close_skill";
        /// <summary>안내한 스킬 말고 다른 스킬 배우기 — 대상은 그 단계의 완료 이벤트 대상.</summary>
        public const string OtherSkills = "other_skills";
        /// <summary>안내한 상품 말고 다른 상품 사기 — 대상은 그 단계의 완료 이벤트 대상(아이템 id).</summary>
        public const string BuyOther = "buy_other";
        /// <summary>이동 — 편지 화면이 조작을 잠그므로 따로 하는 일은 없다.</summary>
        public const string Move = "move";
    }

    /// <summary>기획 CSV의 단계 이름 (phase 열 · complete_event "phase:…") ↔ DayPhase.</summary>
    public static class TutorialPhaseNames
    {
        public static string Of(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Shop: return "shop";
                case DayPhase.Farming: return "farming";
                case DayPhase.CleanDisplay: return "tidy";
                case DayPhase.Sales: return "sales";
                case DayPhase.Settlement: return "settlement";
                case DayPhase.Skill: return "skill";
                default: return phase.ToString().ToLowerInvariant();
            }
        }
    }

    /// <summary>완료 조건 "종류[:대상][:수]". 수가 없으면 1. 마지막 조각이 숫자면 수, 가운데 조각이 대상.</summary>
    public readonly struct TutorialCondition
    {
        public string Kind { get; }
        /// <summary>대상 id (없으면 null).</summary>
        public string Target { get; }
        /// <summary>필요한 양 (하루 누적 개수 · 거리 · 지금 개수).</summary>
        public float Threshold { get; }

        public TutorialCondition(string kind, string target, float threshold)
        {
            Kind = kind ?? string.Empty;
            Target = string.IsNullOrEmpty(target) ? null : target;
            Threshold = threshold;
        }

        public bool IsAuto => Kind == TutorialEvents.Auto;

        /// <summary>누적 키 — 대상이 있으면 "종류:대상", 없으면 "종류".</summary>
        public string Key => TutorialProgress.KeyOf(Kind, Target);

        public static TutorialCondition Parse(string text)
        {
            var parts = (text ?? string.Empty).Trim().Split(':');
            string kind = parts[0].Trim();
            string target = null;
            float threshold = 1f;
            if (parts.Length >= 2)
            {
                string last = parts[parts.Length - 1].Trim();
                if (float.TryParse(last, NumberStyles.Float, CultureInfo.InvariantCulture, out float n))
                {
                    threshold = n;
                    if (parts.Length >= 3)
                        target = parts[1].Trim();
                }
                else
                    target = parts[1].Trim();
            }
            return new TutorialCondition(kind, target, threshold);
        }

        public override string ToString() => Target == null ? $"{Kind}:{Threshold}" : $"{Kind}:{Target}:{Threshold}";
    }

    /// <summary>튜토리얼 단계 한 줄 (tutorial_steps.csv 한 행). 문구 · 강조 · 잠금은 데이터 — 기획 대본으로 바꿔도 코드는 그대로.</summary>
    public sealed class TutorialStep
    {
        public string Id { get; }
        public int Day { get; }
        /// <summary>기획 단계 이름 (shop · farming · tidy · sales · settlement).</summary>
        public string Phase { get; }
        /// <summary>화면 위쪽 한 줄 문구.</summary>
        public string Text { get; }
        /// <summary>강조 대상 (";" 구분, 지금은 데이터만 — 외곽선 · 화살표는 P1).</summary>
        public string Highlight { get; }
        /// <summary>완료 이벤트 원문.</summary>
        public string CompleteEvent { get; }
        /// <summary>잠금 원문 (";" 구분).</summary>
        public string LocksText { get; }

        public TutorialCondition Condition { get; }
        public IReadOnlyList<string> Locks { get; }

        public TutorialStep(string id, int day, string phase, string text, string highlight, string completeEvent, string locks)
        {
            Id = id ?? string.Empty;
            Day = day;
            Phase = phase ?? string.Empty;
            Text = text ?? string.Empty;
            Highlight = highlight ?? string.Empty;
            CompleteEvent = completeEvent ?? string.Empty;
            LocksText = locks ?? string.Empty;
            Condition = TutorialCondition.Parse(CompleteEvent);
            Locks = PlanCsv.SplitList(LocksText);
        }

        public bool HasLock(string lockName)
        {
            foreach (var l in Locks)
                if (l == lockName)
                    return true;
            return false;
        }

        /// <summary>기획 CSV(step_id · day · phase · text · highlight · complete_event · locks · mvp) → 단계 목록. mvp가 phase2인 행은 뺀다.</summary>
        public static List<TutorialStep> FromCsv(CsvTable table)
        {
            var steps = new List<TutorialStep>();
            if (table == null)
                return steps;
            for (int r = 0; r < table.RowCount; r++)
            {
                if (table.Get(r, "mvp") == "phase2")
                    continue;
                string id = table.Get(r, "step_id");
                if (string.IsNullOrEmpty(id))
                    continue;
                CsvValue.TryInt(table.Get(r, "day"), out int day);
                steps.Add(new TutorialStep(id, day, table.Get(r, "phase"), table.Get(r, "text"), table.Get(r, "highlight"),
                    table.Get(r, "complete_event"), table.Get(r, "locks")));
            }
            return steps;
        }

        public override string ToString() => $"{Id} ({Day}일차 {Phase}) {CompleteEvent}";
    }
}
