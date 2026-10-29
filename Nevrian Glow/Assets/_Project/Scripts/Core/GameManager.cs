using System.Collections.Generic;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using UnityEngine;

namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// 일차 · 하루 단계 · 달러 · SP를 가진다 (CLAUDE 핵심 규칙 2: 단계는 이 상태 머신 하나로).
    /// 규칙은 Logic의 DayCycle · Wallet, 이 컴포넌트는 알림(GameEvents)과 로그 연결만 한다.
    /// 구역 씬이 모두 올라온 뒤 타이틀 화면(5-6)이 새 게임 · 이어하기로 시작한다 (타이틀을 끄면 바로 1일차 상점 단계).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour, Logic.Shop.IUnlockContext
    {
        [Header("참조")]
        [Tooltip("시작 자금 (Data/Stats/GameStartSettings)")]
        [SerializeField] private GameStartSettingsData startSettings;
        [Tooltip("구역 씬 로드 완료를 알려 주는 부트스트래퍼 (Core)")]
        [SerializeField] private SceneBootstrapper bootstrapper;
        [Tooltip("경험치 · 레벨 규칙 (Data/Stats/Progression)")]
        [SerializeField] private ProgressionData progressionData;

        [Header("조정값")]
        [Tooltip("타이틀 화면(새 게임 · 이어하기)이 시작을 맡는다. 끄면 구역 씬이 올라오자마자 새 게임 (에디터 빠른 확인용)")]
        [SerializeField] private bool waitForTitle = true;

        [Header("디버그")]
        [Tooltip("현재 일차 (읽기 전용)")]
        [SerializeField] private int day;
        [Tooltip("현재 단계 (읽기 전용 — 바꾸려면 Glow Farm 디버그 창)")]
        [SerializeField] private DayPhase phase;
        [Tooltip("보유 달러 (읽기 전용)")]
        [SerializeField] private int money;
        [Tooltip("보유 SP (읽기 전용)")]
        [SerializeField] private int skillPoints;
        [Tooltip("레벨 (읽기 전용)")]
        [SerializeField] private int level;
        [Tooltip("레벨 안 경험치 / 다음 레벨까지 (읽기 전용)")]
        [SerializeField] private string expView;

        private static GameManager instance;
        private DayCycle cycle;
        private Wallet wallet;
        private Progression progression;
        private readonly SkillLevels skills = new SkillLevels();
        private readonly GameRecord record = new GameRecord();
        private readonly RunSummary run = new RunSummary();

        /// <summary>실행 중인 GameManager. Core 씬이 없으면 null.</summary>
        public static GameManager Instance => instance;

        /// <summary>새 게임이 시작돼 일차 · 단계 · 달러가 정해진 상태.</summary>
        public bool IsStarted => cycle != null;
        public int Day => cycle != null ? cycle.Day : 0;
        public DayPhase Phase => cycle != null ? cycle.Phase : DayPhase.Shop;
        public int Money => wallet != null ? wallet.Money : 0;
        public int SkillPoints => wallet != null ? wallet.SkillPoints : 0;
        public int Level => progression != null ? progression.Level : Progression.FirstLevel;
        public int Exp => progression != null ? progression.Exp : 0;
        public int ExpRequired => progression != null ? progression.Required : 0;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogError("[GameManager] GameManager가 두 개 있음 — Core 씬에 하나만 둔다.", this);
                enabled = false;
                return;
            }
            if (startSettings == null || bootstrapper == null || progressionData == null)
            {
                Debug.LogError("[GameManager] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
            if (bootstrapper != null)
                bootstrapper.Ready -= StartNewGame;
        }

        /// <summary>타이틀 화면이 새 게임 · 이어하기를 기다리는지 (아니면 바로 새 게임).</summary>
        public bool WaitsForTitle => waitForTitle;

        private void Start()
        {
            if (!enabled || waitForTitle)
                return;
            if (bootstrapper.IsReady)
                StartNewGame();
            else
                bootstrapper.Ready += StartNewGame;
        }

        /// <summary>1일차 첫 단계 · 시작 자금으로 새 게임을 시작한다 (타이틀 '새 게임' · 이어하기 직전). 저장 불러오기는 SaveSystem.</summary>
        public void StartNewGame()
        {
            bootstrapper.Ready -= StartNewGame;
            cycle = new DayCycle();
            wallet = new Wallet(startSettings.StartingMoney, 0);
            progression = new Progression(progressionData.ToCurve());
            SyncDebugView();

            var start = new PhaseChange(cycle.Day, cycle.Phase, cycle.Day, cycle.Phase);
            ReportPhase(start);
            GameEvents.RaisePhaseChanged(start);
            GameEvents.RaiseDayChanged(cycle.Day);
            GameEvents.RaiseMoneyChanged(wallet.Money);
            GameEvents.RaiseSkillPointsChanged(wallet.SkillPoints);
            GameEvents.RaiseExpChanged(progression.Exp, progression.Required);
            GameEvents.RaiseNewGameStarted(); // 튜토리얼(5-7)은 새 게임에서만 시작 — 이어하기는 이 뒤에 저장 내용이 덮는다
        }

        // ── 저장 (6-1) ──

        /// <summary>일차 · 단계 · 달러 · SP · 레벨 · 스킬 · 수확 기록 · 날마다 정산을 저장 내용에 담는다.</summary>
        public void CaptureTo(Logic.Save.SaveSnapshot into)
        {
            if (!RequireStarted())
                return;
            into.Day = cycle.Day;
            into.Phase = cycle.Phase;
            into.Money = wallet.Money;
            into.SkillPoints = wallet.SkillPoints;
            into.Level = progression.Level;
            into.Exp = progression.Exp;
            Logic.Save.SaveState.CaptureProgress(skills, record, run, into);
        }

        /// <summary>
        /// 저장 내용으로 상태를 바꾸고 알린다. 단계 알림은 같은 날 안의 전환으로 보낸다(DayChanged 아님 → 밤 처리가 다시 돌지 않음).
        /// 다른 시스템(밭 · 인벤토리 · 상점 …)의 복원은 SaveSystem이 이 다음에 한다.
        /// </summary>
        public void LoadFrom(Logic.Save.SaveSnapshot from)
        {
            bootstrapper.Ready -= StartNewGame;
            cycle = new DayCycle(from.Day, from.Phase);
            wallet = new Wallet(from.Money, from.SkillPoints);
            progression = new Progression(progressionData.ToCurve(), from.Level, from.Exp);
            Logic.Save.SaveState.ApplyProgress(skills, record, run, from);
            SyncDebugView();

            var change = new PhaseChange(cycle.Day, cycle.Phase, cycle.Day, cycle.Phase);
            ReportPhase(change);
            GameEvents.RaisePhaseChanged(change);
            GameEvents.RaiseDayChanged(cycle.Day);
            GameEvents.RaiseMoneyChanged(wallet.Money);
            GameEvents.RaiseSkillPointsChanged(wallet.SkillPoints);
            GameEvents.RaiseExpChanged(progression.Exp, progression.Required);
            foreach (var pair in skills.All)
                GameEvents.RaiseSkillLevelChanged(pair.Key, pair.Value);
        }

        /// <summary>매출로 경험치를 받는다 (정산 3-3: 경험치 = 매출 × 1). 레벨업하면 SP가 더해진다.</summary>
        public ExpGain AddRevenueExp(int revenue) => RequireStarted() ? ApplyExp(progression.AddRevenue(revenue)) : default;

        /// <summary>경험치를 바로 더한다 (디버그 · 이후 다른 경험치 출처).</summary>
        public ExpGain AddExp(int amount) => RequireStarted() ? ApplyExp(progression.AddExp(amount)) : default;

        private ExpGain ApplyExp(ExpGain gain)
        {
            if (gain.Exp <= 0)
                return gain;
            if (gain.SkillPointsGained > 0 && wallet.TryChangeSkillPoints(gain.SkillPointsGained))
                GameEvents.RaiseSkillPointsChanged(wallet.SkillPoints);
            SyncDebugView();
            GameEvents.RaiseExpChanged(progression.Exp, progression.Required);
            if (gain.LeveledUp)
                GameEvents.RaiseLeveledUp(progression.Level, gain.LevelsGained);
            return gain;
        }

        // ── 스킬 단계 · 기록 (상점 해금 조건이 읽는다, IUnlockContext) ──

        public int SkillLevel(string skillId) => skills.LevelOf(skillId);
        public IReadOnlyDictionary<string, int> AllSkillLevels => skills.All;
        public bool HasHarvested(string cropId) => record.HasHarvested(cropId);

        /// <summary>스킬 단계를 정한다. SP 소비 · 선행 조건은 SkillSystem(4-3)이 확인한 뒤 부른다 (지금은 디버그 창).</summary>
        public void SetSkillLevel(string skillId, int level)
        {
            if (skills.SetLevel(skillId, level))
                GameEvents.RaiseSkillLevelChanged(skillId, skills.LevelOf(skillId));
        }

        /// <summary>수확을 기록한다 (밭이 부른다). 처음이면 알린다.</summary>
        public void RecordHarvest(string cropId)
        {
            if (record.RecordHarvest(cropId))
                GameEvents.RaiseCropFirstHarvested(cropId);
        }

        public IEnumerable<string> HarvestedCrops => record.Harvested;

        // ── 한 판 기록 (데모 종료 화면 3-4) ──

        /// <summary>날마다 정산 모음.</summary>
        public RunSummary Run => run;

        /// <summary>데모 마지막 일차 (0 = 종료 화면 없음, GameStartSettings).</summary>
        public int DemoLastDay => startSettings != null ? startSettings.DemoLastDay : 0;

        /// <summary>정산 화면이 그날 정산을 기록한다 (같은 날은 덮어씀).</summary>
        public void RecordSettlement(SettlementReport report) => run.Record(report);

        /// <summary>다음 단계로 (스킬 다음은 다음 날 상점). 각 단계의 "끝내기"가 부른다.</summary>
        public void AdvancePhase()
        {
            if (RequireStarted())
                Apply(cycle.Advance());
        }

        /// <summary>남은 단계를 건너뛰고 다음 날 상점으로 (디버그).</summary>
        public void AdvanceToNextDay()
        {
            if (RequireStarted())
                Apply(cycle.AdvanceToNextDay());
        }

        /// <summary>같은 날 안에서 단계를 바로 바꾼다 (디버그 강제 전환). 같은 단계면 아무 일도 없다.</summary>
        public void ForcePhase(DayPhase target)
        {
            if (RequireStarted() && cycle.TrySetPhase(target, out var change))
                Apply(change);
        }

        /// <summary>달러를 더하거나(양수) 쓴다(음수). 모자라면 false.</summary>
        public bool TryChangeMoney(int delta)
        {
            if (!RequireStarted() || !wallet.TryChangeMoney(delta))
                return false;
            SyncDebugView();
            GameEvents.RaiseMoneyChanged(wallet.Money);
            return true;
        }

        /// <summary>SP를 더하거나(양수) 쓴다(음수). 모자라면 false.</summary>
        public bool TryChangeSkillPoints(int delta)
        {
            if (!RequireStarted() || !wallet.TryChangeSkillPoints(delta))
                return false;
            SyncDebugView();
            GameEvents.RaiseSkillPointsChanged(wallet.SkillPoints);
            return true;
        }

        private void Apply(PhaseChange change)
        {
            SyncDebugView();
            ReportPhase(change);
            GameEvents.RaisePhaseChanged(change);
            if (change.DayChanged)
                GameEvents.RaiseDayChanged(change.Day);
        }

        private void ReportPhase(PhaseChange change)
        {
            if (LogCollector.Instance != null)
                LogCollector.Instance.SetDayAndPhase(change.Day, DayPhaseNames.ToKorean(change.Phase));
        }

        private bool RequireStarted()
        {
            if (cycle != null)
                return true;
            Debug.LogError("[GameManager] 게임이 시작되기 전(구역 씬 로드 중)에 호출됨", this);
            return false;
        }

        private void SyncDebugView()
        {
            day = cycle.Day;
            phase = cycle.Phase;
            money = wallet.Money;
            skillPoints = wallet.SkillPoints;
            if (progression != null)
            {
                level = progression.Level;
                expView = $"{progression.Exp} / {progression.Required}";
            }
        }
    }
}
