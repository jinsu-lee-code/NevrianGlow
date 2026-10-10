using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Game
{
    /// <summary>단계가 한 번 바뀐 기록. 일차가 바뀌었으면 DayChanged (밤 처리는 이때 1회).</summary>
    public readonly struct PhaseChange
    {
        public int PreviousDay { get; }
        public DayPhase PreviousPhase { get; }
        public int Day { get; }
        public DayPhase Phase { get; }
        public bool DayChanged => Day != PreviousDay;

        public PhaseChange(int previousDay, DayPhase previousPhase, int day, DayPhase phase)
        {
            PreviousDay = previousDay;
            PreviousPhase = previousPhase;
            Day = day;
            Phase = phase;
        }
    }

    /// <summary>
    /// 일차 · 하루 단계 상태 머신 (GDD 4-1, CLAUDE 핵심 규칙 2).
    /// 상점 → 농사 → 청소·진열 → 장사 → 정산 → 스킬 → 다음 날 상점. 엔진 의존 없음.
    /// </summary>
    public sealed class DayCycle
    {
        public const int FirstDay = 1;

        private static readonly DayPhase[] order =
        {
            DayPhase.Shop,
            DayPhase.Farming,
            DayPhase.CleanDisplay,
            DayPhase.Sales,
            DayPhase.Settlement,
            DayPhase.Skill,
        };

        /// <summary>하루 단계 진행 순서.</summary>
        public static IReadOnlyList<DayPhase> Order => order;

        public int Day { get; private set; }
        public DayPhase Phase { get; private set; }

        /// <summary>새 게임: 1일차 첫 단계.</summary>
        public DayCycle() : this(FirstDay, order[0]) { }

        /// <summary>저장에서 불러올 때 등 특정 상태에서 시작한다.</summary>
        public DayCycle(int day, DayPhase phase)
        {
            if (day < FirstDay)
                throw new ArgumentOutOfRangeException(nameof(day), day, $"일차는 {FirstDay} 이상");
            if (Array.IndexOf(order, phase) < 0)
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "진행 순서에 없는 단계");
            Day = day;
            Phase = phase;
        }

        /// <summary>다음 단계로. 마지막 단계(스킬) 다음은 다음 날 첫 단계(상점).</summary>
        public PhaseChange Advance()
        {
            int index = Array.IndexOf(order, Phase);
            if (index == order.Length - 1)
                return MoveTo(Day + 1, order[0]);
            return MoveTo(Day, order[index + 1]);
        }

        /// <summary>남은 단계를 건너뛰고 다음 날 첫 단계로 (디버그 "다음 날").</summary>
        public PhaseChange AdvanceToNextDay() => MoveTo(Day + 1, order[0]);

        /// <summary>같은 날 안에서 단계를 바로 바꾼다 (디버그 강제 전환). 같은 단계면 false.</summary>
        public bool TrySetPhase(DayPhase phase, out PhaseChange change)
        {
            change = default;
            if (phase == Phase || Array.IndexOf(order, phase) < 0)
                return false;
            change = MoveTo(Day, phase);
            return true;
        }

        private PhaseChange MoveTo(int day, DayPhase phase)
        {
            var change = new PhaseChange(Day, Phase, day, phase);
            Day = day;
            Phase = phase;
            return change;
        }
    }
}
