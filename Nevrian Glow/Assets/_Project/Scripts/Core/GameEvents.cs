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

        /// <summary>경험치가 바뀔 때 (레벨 안 경험치, 다음 레벨까지 필요량) — HUD 경험치 바.</summary>
        public static event Action<int, int> ExpChanged;

        /// <summary>레벨이 오를 때 (새 레벨, 이번에 오른 레벨 수) — 레벨업 연출 · 튜토리얼 첫 스킬.</summary>
        public static event Action<int, int> LeveledUp;

        /// <summary>스킬 단계가 바뀔 때 (스킬 id, 새 단계) — 상점 해금 · 스킬 효과(4-3).</summary>
        public static event Action<string, int> SkillLevelChanged;

        /// <summary>작물을 처음 수확했을 때 (작물 id) — 해금 조건 "첫 수확".</summary>
        public static event Action<string> CropFirstHarvested;

        /// <summary>능력치 변경(스킬 효과)이 바뀔 때 — 핫바 칸 · 밭 성장 표시 등이 다시 읽는다 (StatSystem 4-3).</summary>
        public static event Action StatsChanged;

        // ── 플레이어가 한 일 (튜토리얼 5-7가 완료 이벤트 이름으로 잇는다 — 보내는 쪽은 각 시스템, 바뀐 것만 알린다) ──

        /// <summary>새 게임이 시작됐을 때 (1일차 첫 단계 알림 뒤). 이어하기는 이 다음 저장 내용이 덮는다.</summary>
        public static event Action NewGameStarted;
        /// <summary>밭 한 칸의 개간이 끝났을 때 (칸 번호).</summary>
        public static event Action<int> CellTilled;
        /// <summary>씨앗을 심었을 때 (작물 id).</summary>
        public static event Action<string> CropPlanted;
        /// <summary>물을 줬을 때 (물이 들어간 칸 수).</summary>
        public static event Action<int> CellsWatered;
        /// <summary>수확했을 때 (작물 id, 개수).</summary>
        public static event Action<string, int> CropHarvested;
        /// <summary>쓰레기통에 버렸을 때 (버린 개수).</summary>
        public static event Action<int> TrashDisposed;
        /// <summary>얼룩이 다 닦였을 때 (이번에 다 닦인 얼룩 수).</summary>
        public static event Action<int> StainsCleared;
        /// <summary>진열대에 놓았을 때 (아이템 id).</summary>
        public static event Action<string> ItemDisplayed;
        /// <summary>핫바 칸을 골랐을 때 — 같은 칸을 다시 눌러도 온다 (든 아이템 id, 빈 칸이면 null).</summary>
        public static event Action<string> HotbarSelected;
        /// <summary>편지를 닫았을 때 (편지 id).</summary>
        public static event Action<string> LetterClosed;
        /// <summary>지역에 도착했을 때 (지역 id) — 하루 시작 위치 포함.</summary>
        public static event Action<string> ZoneArrived;
        /// <summary>장사가 끝났을 때 (정산 단계로 넘어가기 직전).</summary>
        public static event Action SalesFinished;
        /// <summary>
        /// 데모 마지막 날 정산에서 '데모 결과 보기'를 눌렀을 때 (일차) — 데모 종료 화면이 열리기 직전.
        /// 엔딩 연출(아트 EndingDirector · LighthouseGoal.PlayEnding, 데모 스토리 충돌 해결안 2번)이 받는 자리.
        /// </summary>
        public static event Action<int> DemoEndReached;

        internal static void RaiseNewGameStarted() => NewGameStarted?.Invoke();
        internal static void RaiseCellTilled(int cellIndex) => CellTilled?.Invoke(cellIndex);
        internal static void RaiseCropPlanted(string cropId) => CropPlanted?.Invoke(cropId);
        internal static void RaiseCellsWatered(int count) => CellsWatered?.Invoke(count);
        internal static void RaiseCropHarvested(string cropId, int count) => CropHarvested?.Invoke(cropId, count);
        internal static void RaiseTrashDisposed(int count) => TrashDisposed?.Invoke(count);
        internal static void RaiseStainsCleared(int count) => StainsCleared?.Invoke(count);
        internal static void RaiseItemDisplayed(string itemId) => ItemDisplayed?.Invoke(itemId);
        internal static void RaiseHotbarSelected(string itemId) => HotbarSelected?.Invoke(itemId);
        internal static void RaiseLetterClosed(string letterId) => LetterClosed?.Invoke(letterId);
        internal static void RaiseZoneArrived(string zoneId) => ZoneArrived?.Invoke(zoneId);
        internal static void RaiseSalesFinished() => SalesFinished?.Invoke();
        internal static void RaiseDemoEndReached(int day) => DemoEndReached?.Invoke(day);

        internal static void RaiseStatsChanged() => StatsChanged?.Invoke();
        internal static void RaiseSkillLevelChanged(string skillId, int level) => SkillLevelChanged?.Invoke(skillId, level);
        internal static void RaiseCropFirstHarvested(string cropId) => CropFirstHarvested?.Invoke(cropId);
        internal static void RaisePhaseChanged(PhaseChange change) => PhaseChanged?.Invoke(change);
        internal static void RaiseDayChanged(int day) => DayChanged?.Invoke(day);
        internal static void RaiseMoneyChanged(int money) => MoneyChanged?.Invoke(money);
        internal static void RaiseSkillPointsChanged(int skillPoints) => SkillPointsChanged?.Invoke(skillPoints);
        internal static void RaiseExpChanged(int exp, int required) => ExpChanged?.Invoke(exp, required);
        internal static void RaiseLeveledUp(int level, int levelsGained) => LeveledUp?.Invoke(level, levelsGained);

        // 도메인 리로드를 끈 플레이 모드 설정에서도 이전 플레이의 구독자가 남지 않게 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubscribers()
        {
            PhaseChanged = null;
            DayChanged = null;
            MoneyChanged = null;
            SkillPointsChanged = null;
            ExpChanged = null;
            LeveledUp = null;
            SkillLevelChanged = null;
            CropFirstHarvested = null;
            StatsChanged = null;
            NewGameStarted = null;
            CellTilled = null;
            CropPlanted = null;
            CellsWatered = null;
            CropHarvested = null;
            TrashDisposed = null;
            StainsCleared = null;
            ItemDisplayed = null;
            HotbarSelected = null;
            LetterClosed = null;
            ZoneArrived = null;
            SalesFinished = null;
            DemoEndReached = null;
        }
    }
}
