using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 데모 종료 화면 (GDD 열린 항목 5번, 결정 2026-10-11: 종료 화면 + 계속). 데모 마지막 날 정산의 버튼이 연다.
    /// 그동안의 요약(RunSummary) · '계속 플레이'(다음 날로) · '게임 종료'. Esc · 다른 화면으로는 닫히지 않는다.
    /// </summary>
    public class DemoEndScreen : GameScreen
    {
        [Header("참조")]
        [Tooltip("작물 표시 이름 (Data/ContentDatabase)")]
        [SerializeField] private ContentDatabase database;
        [Tooltip("제목")]
        [SerializeField] private TMP_Text titleLabel;
        [Tooltip("본문 (줄마다 한 항목)")]
        [SerializeField] private TMP_Text bodyLabel;
        [Tooltip("'계속 플레이' 버튼 — 다음 날로")]
        [SerializeField] private Button continueButton;
        [Tooltip("'게임 종료' 버튼")]
        [SerializeField] private Button quitButton;

        private bool closing;

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

        private void OnEnable() => GameEvents.PhaseChanged += OnPhaseChanged;
        private void OnDisable() => GameEvents.PhaseChanged -= OnPhaseChanged;

        // 디버그로 정산 단계를 벗어나면 같이 닫는다
        private void OnPhaseChanged(PhaseChange change)
        {
            if (IsOpen && change.Phase != DayPhase.Settlement)
                ForceClose();
        }

        public void Open() => Show();

        protected override void OnOpened()
        {
            closing = false;
            var game = GameManager.Instance;
            if (game == null)
            {
                titleLabel.text = "데모 끝";
                bodyLabel.text = string.Empty;
                return;
            }
            titleLabel.text = $"데모 끝 - {game.Day}일 동안의 가게";
            var names = new List<string>();
            foreach (var id in game.HarvestedCrops)
            {
                var crop = database.Get<CropData>(id);
                names.Add(crop != null ? crop.DisplayName : id);
            }
            names.Sort(string.CompareOrdinal);
            string body = string.Join("\n", game.Run.Lines(game.Level, game.Money, names));
            bodyLabel.text = body + "\n\n플레이해 주셔서 고맙습니다! 계속 플레이하면 다음 날로 넘어갑니다.";
        }

        protected override bool OnClosing() => closing;

        /// <summary>'계속 플레이': 닫고 다음 날로 (데모 범위 밖 — 수치는 데모 기준).</summary>
        public void Continue()
        {
            if (!IsOpen)
                return;
            ForceClose();
            if (GameManager.Instance != null)
                GameManager.Instance.AdvanceToNextDay();
        }

        /// <summary>'게임 종료'. 저장(6-1)이 생기면 먼저 저장한다.</summary>
        public void Quit()
        {
            if (!IsOpen)
                return;
            Debug.Log("[DemoEndScreen] 게임 종료");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ForceClose()
        {
            closing = true;
            Close();
            closing = false;
        }
    }
}
