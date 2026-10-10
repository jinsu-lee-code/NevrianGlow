using System;
using NevrianGlowFarm.Logic.Game;
using UnityEngine;

namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// 시스템 사이의 알림 (CLAUDE 코딩 규칙: 이벤트로 결합도를 낮추고 바뀐 것만 알린다).
    /// 보내는 쪽은 GameManager 하나. 받는 쪽은 OnEnable에서 구독, OnDisable에서 해제한다.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>단계가 바뀔 때. 일차가 바뀌었으면 DayChanged보다 먼저 온다.</summary>
        public static event Action<PhaseChange> PhaseChanged;

        /// <summary>
        /// 일차가 바뀔 때 1회 (새 일차). 새 게임 시작 때도 초기값으로 1회 온다 (HUD 표시용).
        /// 밤 처리처럼 "날이 넘어갈 때만" 할 일은 PhaseChanged의 DayChanged로 판단한다.
        /// </summary>
        public static event Action<int> DayChanged;

        /// <summary>달러가 바뀔 때 (새 값).</summary>
        public static event Action<int> MoneyChanged;

        /// <summary>SP가 바뀔 때 (새 값).</summary>
        public static event Action<int> SkillPointsChanged;

        internal static void RaisePhaseChanged(PhaseChange change) => PhaseChanged?.Invoke(change);
        internal static void RaiseDayChanged(int day) => DayChanged?.Invoke(day);
        internal static void RaiseMoneyChanged(int money) => MoneyChanged?.Invoke(money);
        internal static void RaiseSkillPointsChanged(int skillPoints) => SkillPointsChanged?.Invoke(skillPoints);

        // 도메인 리로드를 끈 플레이 모드 설정에서도 이전 플레이의 구독자가 남지 않게 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubscribers()
        {
            PhaseChanged = null;
            DayChanged = null;
            MoneyChanged = null;
            SkillPointsChanged = null;
        }
    }
}
