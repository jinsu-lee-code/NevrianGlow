using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 데모 플레이 종료 창 (등대시스템 기획서 v2 7절, 10/30): 데모 마지막 날 정산 뒤 엔딩(아트 EndingDirector)이 끝나면 연다.
    /// '이어서 진행하기' = 다음 날 아침으로(등대는 켜진 채), '게임 종료하기' = 다음 날 아침으로 넘겨 자동 저장한 뒤 종료. 기본 선택은 '이어서 진행하기'.
    /// Esc · 다른 화면으로는 닫히지 않는다. 데모 마지막 날 다음부터는 등대가 끝까지 켜진 채로 (엔딩 뒤 상태 유지 — 불러와도).
    /// </summary>
    public class DemoEndScreen : GameScreen
    {
        [Header("참조")]
        [Tooltip("작물 표시 이름 (Data/ContentDatabase)")]
        [SerializeField] private ContentDatabase database;
        [Tooltip("제목")]
        [SerializeField] private TMP_Text titleLabel;
        [Tooltip("본문")]
        [SerializeField] private TMP_Text bodyLabel;
        [Tooltip("'이어서 진행하기' 버튼 — 다음 날로")]
        [SerializeField] private Button continueButton;
        [Tooltip("'게임 종료하기' 버튼 — 저장 후 종료")]
        [SerializeField] private Button quitButton;

        [Header("조정값")]
        [Tooltip("제목 (기획서 v2 7절)")]
        [SerializeField] private string title = "데모 플레이 종료";
        [Tooltip("본문 (기획서 v2 7절)")]
        [SerializeField, TextArea] private string message = "글로우 팜의 등대에 다시 불이 들어왔어요.\n플레이해 주셔서 고마워요.";
        [Tooltip("이어서 버튼 글자")]
        [SerializeField] private string continueText = "이어서 진행하기";
        [Tooltip("종료 버튼 글자")]
        [SerializeField] private string quitText = "게임 종료하기";

        private bool closing;
        private bool keepLitPending = true;

        public static DemoEndScreen Instance { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (database == null || titleLabel == null || bodyLabel == null || continueButton == null || quitButton == null)
            {
                Debug.LogError("[DemoEndScreen] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            Instance = this;
            continueButton.onClick.AddListener(Continue);
            quitButton.onClick.AddListener(Quit);
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
            GameEvents.DayChanged += OnDayChanged;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            GameEvents.PhaseChanged -= OnPhaseChanged;
            GameEvents.DayChanged -= OnDayChanged;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // 디버그로 정산 단계를 벗어나면 같이 닫는다
        private void OnPhaseChanged(PhaseChange change)
        {
            if (IsOpen && change.Phase != DayPhase.Settlement)
                ForceClose();
        }

        private void OnDayChanged(int day) => keepLitPending = true;
        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => keepLitPending = true;

        // 엔딩 뒤(데모 마지막 날 다음부터): 등대를 끝까지 켜 둔다 — 아트 등대 연결은 엔딩 전까지 심장을 끈 채(0.99)로 맞추므로,
        // 날이 바뀌거나 씬이 올라온 다음 프레임 끝에 한 번 덮는다 (아트 스크립트는 그대로)
        private void LateUpdate()
        {
            if (!keepLitPending)
                return;
            var game = GameManager.Instance;
            var tower = LighthouseGoal.Active;
            if (game == null || !game.IsStarted || tower == null)
                return;
            keepLitPending = false;
            if (game.Day > game.DemoLastDay && !tower.IsEnding && tower.ShownProgress < 1f)
                tower.SetProgressImmediate(1f);
        }

        public void Open() => Show();

        protected override void OnOpened()
        {
            closing = false;
            titleLabel.text = title;
            bodyLabel.text = message;
            SetButtonText(continueButton, continueText);
            SetButtonText(quitButton, quitText);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(continueButton.gameObject); // 기본 선택 = 이어서 진행하기
        }

        private static void SetButtonText(Button button, string text)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = text;
        }

        protected override bool OnClosing() => closing;

        /// <summary>'이어서 진행하기': 닫고 다음 날 아침으로 (데모 범위 밖 — 수치는 데모 기준, 등대는 켜진 채).</summary>
        public void Continue()
        {
            if (!IsOpen)
                return;
            ForceClose();
            if (GameManager.Instance != null)
                GameManager.Instance.AdvanceToNextDay();
        }

        /// <summary>'게임 종료하기': 다음 날 아침으로 넘겨(밤 처리) 그 아침을 저장하고 종료 — 다시 하면 엔딩 뒤 아침부터.</summary>
        public void Quit()
        {
            if (!IsOpen)
                return;
            ForceClose();
            if (GameManager.Instance != null)
                GameManager.Instance.AdvanceToNextDay();
            if (Systems.Save.SaveSystem.Instance != null)
                Systems.Save.SaveSystem.Instance.Save(out _);
            GameFlow.Quit();
        }

        private void ForceClose()
        {
            closing = true;
            Close();
            closing = false;
        }
    }
}
