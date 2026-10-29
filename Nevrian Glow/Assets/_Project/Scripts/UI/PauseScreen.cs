using NevrianGlowFarm.Core;
using NevrianGlowFarm.Systems.Save;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 일시정지 (Esc, TASKS 5-6). 다른 화면이 열려 있지 않을 때만 열린다 — 열린 화면의 Esc는 그 화면 닫기.
    /// 열리면 게임 시간을 멈춘다(timeScale 0). 계속 · 설정 · 타이틀로 · 게임 종료.
    /// 수동 저장은 없다 (결정 2026-10-21): 마지막 자동 저장(그날 아침)을 보여 주고, 타이틀로 · 종료는 "오늘 진행은 사라짐"을 확인받는다.
    /// </summary>
    public class PauseScreen : GameScreen
    {
        [Header("참조")]
        [Tooltip("마지막 저장 확인 (Core Save)")]
        [SerializeField] private SaveSystem saveSystem;
        [Tooltip("버튼 묶음 — 설정 · 확인 창을 띄울 때 숨긴다")]
        [SerializeField] private GameObject menu;
        [Tooltip("'계속' 버튼")]
        [SerializeField] private Button resumeButton;
        [Tooltip("'설정' 버튼")]
        [SerializeField] private Button settingsButton;
        [Tooltip("'타이틀로' 버튼")]
        [SerializeField] private Button titleButton;
        [Tooltip("'게임 종료' 버튼")]
        [SerializeField] private Button quitButton;
        [Tooltip("저장 안내 (마지막 저장: N일차 아침)")]
        [SerializeField] private TMP_Text saveInfoLabel;
        [Tooltip("설정 패널")]
        [SerializeField] private SettingsView settings;
        [Tooltip("확인 창")]
        [SerializeField] private ConfirmDialog confirm;
        [Tooltip("열기 · 닫기 입력 (UI/Cancel, Esc)")]
        [SerializeField] private InputActionReference cancelAction;

        private float timeScaleBefore = 1f;

        public static PauseScreen Instance { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (saveSystem == null || menu == null || resumeButton == null || settingsButton == null || titleButton == null || quitButton == null
                || saveInfoLabel == null || settings == null || confirm == null || cancelAction == null)
            {
                Debug.LogError("[PauseScreen] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            Instance = this;
            resumeButton.onClick.AddListener(Close);
            settingsButton.onClick.AddListener(OpenSettings);
            titleButton.onClick.AddListener(AskReturnToTitle);
            quitButton.onClick.AddListener(AskQuit);
        }

        protected override void OnDestroy()
        {
            if (IsOpen)
                Time.timeScale = timeScaleBefore;
            base.OnDestroy();
            if (Instance == this)
                Instance = null;
        }

        private void OnEnable() => cancelAction.action.Enable();

        private void Update()
        {
            if (!cancelAction.action.WasPressedThisFrame())
                return;
            if (IsOpen)
            {
                if (confirm.IsOpen)
                    confirm.Cancel();
                else if (settings.IsOpen)
                    settings.Back();
                else
                    Close();
                return;
            }
            // 다른 화면이 열려 있거나 이번 프레임에 Esc로 닫혔으면 열지 않는다. 시작 전(타이틀)에도 열지 않는다.
            // 튜토리얼이 닫기를 잠근 스킬 트리(5-7) 위에는 연다 — 그 화면은 Esc를 받지 않고, 일시정지를 닫으면 튜토리얼이 다시 연다
            var game = GameManager.Instance;
            bool overLocked = Current is SkillScreen skill && skill.IsCloseLocked;
            if ((Current == null || overLocked) && !ClosedThisFrame && game != null && game.IsStarted)
                Open();
        }

        public void Open() => Show();

        protected override void OnOpened()
        {
            timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            settings.Hide();
            confirm.Hide();
            menu.SetActive(true);
            saveInfoLabel.text = SaveLine() + "\n오늘 한 일은 다음 날 아침에 자동 저장됩니다.";
        }

        protected override void OnClosed()
        {
            settings.Hide();
            confirm.Hide();
            Time.timeScale = timeScaleBefore;
        }

        private string SaveLine()
        {
            if (saveSystem.TryPeek(out _, out string message))
                return $"마지막 저장: {message}";
            return saveSystem.HasSave ? message : "저장 없음";
        }

        private void OpenSettings()
        {
            if (!IsOpen)
                return;
            menu.SetActive(false);
            settings.Open(() => menu.SetActive(true));
        }

        private void AskReturnToTitle()
        {
            if (!IsOpen)
                return;
            menu.SetActive(false);
            confirm.Ask(LoseTodayText("타이틀로 갈까요?"), "타이틀로", GameFlow.ReturnToTitle, () => menu.SetActive(true));
        }

        private void AskQuit()
        {
            if (!IsOpen)
                return;
            menu.SetActive(false);
            confirm.Ask(LoseTodayText("게임을 끝낼까요?"), "게임 종료", GameFlow.Quit, () => menu.SetActive(true));
        }

        private string LoseTodayText(string question)
        {
            bool saved = saveSystem.TryPeek(out int day, out _);
            return saved
                ? $"{question}\n오늘 한 일은 저장되지 않고, 다음에는 {day}일차 아침부터 이어집니다."
                : $"{question}\n저장된 진행이 없어 처음부터 다시 시작합니다.";
        }
    }
}
