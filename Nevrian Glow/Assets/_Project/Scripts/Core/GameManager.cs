using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using UnityEngine;

namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// 일차 · 하루 단계 · 달러 · SP를 가진다 (CLAUDE 핵심 규칙 2: 단계는 이 상태 머신 하나로).
    /// 규칙은 Logic의 DayCycle · Wallet, 이 컴포넌트는 알림(GameEvents)과 로그 연결만 한다.
    /// 구역 씬이 모두 올라온 뒤(SceneBootstrapper.Ready) 1일차 상점 단계로 시작한다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("시작 자금 (Data/Stats/GameStartSettings)")]
        [SerializeField] private GameStartSettingsData startSettings;
        [Tooltip("구역 씬 로드 완료를 알려 주는 부트스트래퍼 (Core)")]
        [SerializeField] private SceneBootstrapper bootstrapper;

        [Header("디버그")]
        [Tooltip("현재 일차 (읽기 전용)")]
        [SerializeField] private int day;
        [Tooltip("현재 단계 (읽기 전용 — 바꾸려면 Glow Farm 디버그 창)")]
        [SerializeField] private DayPhase phase;
        [Tooltip("보유 달러 (읽기 전용)")]
        [SerializeField] private int money;
        [Tooltip("보유 SP (읽기 전용)")]
        [SerializeField] private int skillPoints;

        private static GameManager instance;
        private DayCycle cycle;
        private Wallet wallet;

        /// <summary>실행 중인 GameManager. Core 씬이 없으면 null.</summary>
        public static GameManager Instance => instance;

        /// <summary>새 게임이 시작돼 일차 · 단계 · 달러가 정해진 상태.</summary>
        public bool IsStarted => cycle != null;
        public int Day => cycle != null ? cycle.Day : 0;
        public DayPhase Phase => cycle != null ? cycle.Phase : DayPhase.Shop;
        public int Money => wallet != null ? wallet.Money : 0;
        public int SkillPoints => wallet != null ? wallet.SkillPoints : 0;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogError("[GameManager] GameManager가 두 개 있음 — Core 씬에 하나만 둔다.", this);
                enabled = false;
                return;
            }
            if (startSettings == null || bootstrapper == null)
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

        private void Start()
        {
            if (!enabled)
                return;
            if (bootstrapper.IsReady)
                StartNewGame();
            else
                bootstrapper.Ready += StartNewGame;
        }

        /// <summary>1일차 첫 단계 · 시작 자금으로 새 게임을 시작한다. (저장 불러오기는 6-1)</summary>
        public void StartNewGame()
        {
            bootstrapper.Ready -= StartNewGame;
            cycle = new DayCycle();
            wallet = new Wallet(startSettings.StartingMoney, 0);
            SyncDebugView();

            var start = new PhaseChange(cycle.Day, cycle.Phase, cycle.Day, cycle.Phase);
            ReportPhase(start);
            GameEvents.RaisePhaseChanged(start);
            GameEvents.RaiseDayChanged(cycle.Day);
            GameEvents.RaiseMoneyChanged(wallet.Money);
            GameEvents.RaiseSkillPointsChanged(wallet.SkillPoints);
        }

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
        }
    }
}
