using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Systems.Cleaning;
using NevrianGlowFarm.Systems.Sales;
using NevrianGlowFarm.Systems.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 정산 화면 (GDD 4-1: 손님 · 판매 · 매출 · 지출 · 순이익 · 경험치). 정산 단계에 들어서면 저절로 열린다.
    /// Esc · 다른 화면으로는 닫히지 않고 '다음 날' 버튼만 — 누르면 다음 날로 넘기고(밤 처리 · 상점 입고는 날 변경에 붙어 있음)
    /// ZoneTransition이 같은 페이드로 농장에 옮긴다. 디버그로 정산 단계를 벗어나면 같이 닫힌다.
    /// </summary>
    public class SettlementScreen : GameScreen, Systems.Tutorial.ITutorialHighlightSource
    {
        [Header("참조")]
        [Tooltip("오늘 상점 지출 (Core Shop)")]
        [SerializeField] private ShopSystem shop;
        [Tooltip("작물 표시 이름 (Data/ContentDatabase)")]
        [SerializeField] private ContentDatabase database;
        [Tooltip("제목 (n일차 정산)")]
        [SerializeField] private TMP_Text titleLabel;
        [Tooltip("본문 (줄마다 한 항목)")]
        [SerializeField] private TMP_Text bodyLabel;
        [Tooltip("'다음 날' 버튼")]
        [SerializeField] private Button nextDayButton;
        [Tooltip("'다음 날' 버튼 글자 (데모 마지막 날에는 '데모 결과 보기')")]
        [SerializeField] private TMP_Text nextDayLabel;
        [Tooltip("데모 종료 화면 (Core UICanvas/DemoEndScreen)")]
        [SerializeField] private DemoEndScreen demoEndScreen;

        [Header("조정값")]
        [Tooltip("평소 버튼 글자")]
        [SerializeField] private string nextDayText = "다음 날";
        [Tooltip("데모 마지막 날 버튼 글자")]
        [SerializeField] private string demoEndText = "데모 결과 보기";

        private bool advancing;
        private bool pending;
        private bool yielding;

        public static SettlementScreen Instance { get; private set; }

        /// <summary>정산 단계라 열릴 차례인데 아직 안 열렸는지 (다른 화면이 닫히기를 기다리는 중 포함).</summary>
        public bool IsPending => pending && !IsOpen;

        /// <summary>지금 보여 주는 정산 (열린 동안 유효).</summary>
        public SettlementReport Report { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (shop == null || database == null || titleLabel == null || bodyLabel == null || nextDayButton == null || nextDayLabel == null || demoEndScreen == null)
            {
                Debug.LogError("[SettlementScreen] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            Instance = this;
            nextDayButton.onClick.AddListener(NextDay);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
                Instance = null;
        }

        private void OnEnable()
        {
            GameEvents.PhaseChanged += OnPhaseChanged;
            Systems.Tutorial.TutorialHighlights.Register(this);
        }

        private void OnDisable()
        {
            GameEvents.PhaseChanged -= OnPhaseChanged;
            Systems.Tutorial.TutorialHighlights.Unregister(this);
        }

        // 튜토리얼 강조 (5-7): btn_next_day = 다음 날 버튼, settle_* = 정산 본문 (열려 있을 때만)
        public bool TryGetHighlight(string name, string arg, out Systems.Tutorial.HighlightHit hit)
        {
            hit = default;
            if (!IsOpen)
                return false;
            if (name == "btn_next_day")
                hit = Systems.Tutorial.HighlightHit.On(nextDayButton.GetComponent<RectTransform>());
            else if (name.StartsWith("settle_"))
                hit = Systems.Tutorial.HighlightHit.On(bodyLabel.rectTransform);
            else
                return false;
            return true;
        }

        private void OnPhaseChanged(PhaseChange change)
        {
            if (change.Phase == DayPhase.Settlement)
                pending = true;
            else
            {
                pending = false;
                yielding = false;
                if (IsOpen)
                    ForceClose();
            }
        }

        private void Update()
        {
            if (!pending || IsOpen)
                return;
            // 다른 화면(가방 등)이 열려 있으면 먼저 닫아 본다 — 닫을 수 없는 상태면 다음 프레임에 다시.
            // 비켜 준 동안(튜토리얼이 연 스킬 트리 · 편지)은 닫지 않고 그 화면이 스스로 닫히기를 기다린다
            if (Current != null && !yielding)
                Current.Close();
            if (Current == null)
                Open();
        }

        /// <summary>오늘 정산을 만들어 연다 (정산 단계가 아니어도 디버그로 열 수 있다).</summary>
        public void Open()
        {
            pending = false;
            yielding = false;
            Show();
        }

        protected override void OnOpened()
        {
            advancing = false;
            Report = BuildReport();
            var game = GameManager.Instance;
            titleLabel.text = $"{Report.Day}일차 정산";
            // 레벨업으로 새로 열린 상품 (스킬트리 폐기 10/29)
            var shop = Systems.Shop.ShopSystem.Instance;
            var unlocked = shop != null && Report.Exp.LeveledUp ? shop.UnlockedBetween(Report.Level - Report.Exp.LevelsGained, Report.Level) : null;
            string body = string.Join("\n", Report.Lines(NameOf, unlocked));
            string grades = GradeLine();
            if (grades != null)
                body += "\n" + grades;
            if (game != null)
            {
                body += $"\n\n보유 {game.Money:N0}달러";
                game.RecordSettlement(Report);
            }
            bodyLabel.text = body;
            nextDayLabel.text = IsDemoEnd ? demoEndText : nextDayText;
        }

        private bool IsDemoEnd => GameManager.Instance != null && RunSummary.IsDemoLastDay(Report.Day, GameManager.Instance.DemoLastDay);

        // '다음 날'로만 닫힌다 (Esc · 다른 화면 열기로는 안 닫힘)
        protected override bool OnClosing() => advancing;

        /// <summary>'다음 날': 화면을 닫고 다음 날로 (농장 이동은 ZoneTransition).
        /// 데모 마지막 날이면 대신 데모 종료 화면을 연다 (거기서 '계속 플레이' = 다음 날).</summary>
        public void NextDay()
        {
            if (!IsOpen)
                return;
            bool demoEnd = IsDemoEnd;
            ForceClose();
            if (demoEnd)
                demoEndScreen.Open();
            else if (GameManager.Instance != null)
                GameManager.Instance.AdvanceToNextDay();
        }

        private void ForceClose()
        {
            advancing = true;
            Close();
            advancing = false;
        }

        /// <summary>
        /// 잠깐 비켜 준다 (튜토리얼 5-7: 정산 위에 스킬 트리 · 편지를 연다) — 다른 화면이 닫히면 정산이 다시 뜬다 (같은 날 정산이면 다시 기록하지 않음).
        /// </summary>
        public void CloseTemporarily()
        {
            if (!IsOpen)
                return;
            ForceClose();
            pending = GameManager.Instance != null && GameManager.Instance.Phase == DayPhase.Settlement;
            yielding = pending;
        }

        private SettlementReport BuildReport()
        {
            var game = GameManager.Instance;
            int day = game != null ? game.Day : 0;
            int level = game != null ? game.Level : Progression.FirstLevel;
            int spent = shop.SpentToday;
            var sales = SalesSystem.Instance;
            // 오늘 장사 결과만 (디버그로 장사 없이 정산에 오면 지난 결과가 남아 있을 수 있다)
            if (sales != null && sales.LastResult.HasValue && sales.LastResult.Value.Day == day)
            {
                var r = sales.LastResult.Value;
                return new SettlementReport(day, true, r.Customers, r.Buyers, r.EmptyHanded, r.ItemsSold, r.Revenue, r.Sold, spent, r.Exp, level);
            }
            return new SettlementReport(day, false, 0, 0, 0, 0, 0, null, spent, default, level);
        }

        // 청결 · 정돈 등급 (정산 표시 P0, 회신2 2-4) — 장사 시작 때 확정한 값. 장사를 안 했으면 없음.
        private static string GradeLine()
        {
            var cleaning = CleaningSystem.Instance;
            if (cleaning == null || cleaning.LockedCleanliness < 0f || cleaning.LockedTidiness < 0f)
                return null;
            float clean = cleaning.LockedCleanliness, tidy = cleaning.LockedTidiness;
            return $"청결 등급 {cleaning.GradeOf(clean)} ({clean:P0}) · 정돈 등급 {cleaning.GradeOf(tidy)} ({tidy:P0})";
        }

        private string NameOf(string cropId)
        {
            var crop = database.Get<CropData>(cropId);
            return crop != null ? crop.DisplayName : null;
        }
    }
}
