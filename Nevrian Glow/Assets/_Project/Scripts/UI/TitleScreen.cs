using NevrianGlowFarm.Core;
using NevrianGlowFarm.Systems.Save;
using NevrianGlowFarm.Systems.Zones;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 타이틀 화면 (TASKS 5-6, 결정 2026-10-21: Core 씬 위 화면). 구역 씬이 다 올라오면 농장 풍경 위에 뜬다.
    /// 이어하기(저장된 날 아침부터) · 새 게임(저장이 있으면 확인 — 1일차 아침으로 덮어씀) · 설정 · 종료. 시작하기 전에는 닫히지 않는다.
    /// GameManager의 '타이틀 기다림'을 끄면 뜨지 않는다 (에디터 빠른 확인).
    /// </summary>
    public class TitleScreen : GameScreen
    {
        [Header("참조")]
        [Tooltip("구역 씬 로드 완료 (Core SceneBootstrapper)")]
        [SerializeField] private SceneBootstrapper bootstrapper;
        [Tooltip("저장 파일 확인 · 불러오기 (Core Save)")]
        [SerializeField] private SaveSystem saveSystem;
        [Tooltip("버튼 묶음 — 설정 · 확인 창을 띄울 때 숨긴다")]
        [SerializeField] private GameObject menu;
        [Tooltip("'이어하기' 버튼")]
        [SerializeField] private Button continueButton;
        [Tooltip("'새 게임' 버튼")]
        [SerializeField] private Button newGameButton;
        [Tooltip("'설정' 버튼")]
        [SerializeField] private Button settingsButton;
        [Tooltip("'종료' 버튼")]
        [SerializeField] private Button quitButton;
        [Tooltip("저장 안내 한 줄 (이어하기: N일차 아침 · 저장 없음 · 깨짐)")]
        [SerializeField] private TMP_Text saveInfoLabel;
        [Tooltip("설정 패널")]
        [SerializeField] private SettingsView settings;
        [Tooltip("확인 창")]
        [SerializeField] private ConfirmDialog confirm;
        [Tooltip("뒤로 입력 (UI/Cancel, Esc) — 설정 · 확인 창 닫기")]
        [SerializeField] private InputActionReference cancelAction;
        [Tooltip("타이틀이 떠 있는 동안 숨길 게임 화면 요소 (HUD · 핫바 · 조준점)")]
        [SerializeField] private GameObject[] hideWhileOpen = new GameObject[0];
        [Tooltip("메뉴와 함께 보이고 숨는 것 — 로고 · 부제 (설정 · 확인 창이 열리면 숨김, 5-4b)")]
        [SerializeField] private GameObject[] showWithMenu = new GameObject[0];
        [Tooltip("새 게임 때 한 번 띄울 오프닝 편지 (P-7) — 비우면 바로 상점")]
        [SerializeField] private OpeningLetterScreen openingLetter;

        private bool starting;
        private bool hasSave;
        private int savedDay;

        protected override void Awake()
        {
            base.Awake();
            if (bootstrapper == null || saveSystem == null || menu == null || continueButton == null || newGameButton == null || settingsButton == null
                || quitButton == null || saveInfoLabel == null || settings == null || confirm == null || cancelAction == null)
            {
                Debug.LogError("[TitleScreen] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            continueButton.onClick.AddListener(ContinueGame);
            newGameButton.onClick.AddListener(NewGame);
            settingsButton.onClick.AddListener(OpenSettings);
            quitButton.onClick.AddListener(GameFlow.Quit);
        }

        private void OnEnable() => cancelAction.action.Enable();

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (bootstrapper != null)
                bootstrapper.Ready -= Open;
        }

        private void Start()
        {
            if (!enabled || GameManager.Instance == null || !GameManager.Instance.WaitsForTitle)
                return;
            if (bootstrapper.IsReady)
                Open();
            else
                bootstrapper.Ready += Open;
        }

        private void Update()
        {
            if (!IsOpen || !cancelAction.action.WasPressedThisFrame())
                return;
            if (confirm.IsOpen)
                confirm.Cancel();
            else if (settings.IsOpen)
                settings.Back();
        }

        public void Open()
        {
            bootstrapper.Ready -= Open;
            Show();
        }

        protected override void OnOpened()
        {
            starting = false;
            settings.Hide();
            confirm.Hide();
            SetMenuVisible(true);
            SetGameHudVisible(false);
            RefreshSave();
            AudioManager.PlayMusic(SoundCues.TitleMusic);
        }

        private void SetMenuVisible(bool visible)
        {
            menu.SetActive(visible);
            foreach (var go in showWithMenu)
                if (go != null)
                    go.SetActive(visible);
        }

        private void SetGameHudVisible(bool visible)
        {
            foreach (var go in hideWhileOpen)
                if (go != null)
                    go.SetActive(visible);
        }

        protected override bool OnClosing() => starting;

        protected override void OnClosed()
        {
            SetGameHudVisible(true);
            // 타이틀 음악 → 지금 있는 구역 · 단계의 배경음
            if (ZoneTransition.Instance != null)
                ZoneTransition.Instance.UpdateMusic();
        }

        private void RefreshSave()
        {
            hasSave = saveSystem.TryPeek(out savedDay, out string message);
            continueButton.interactable = hasSave;
            if (hasSave)
                saveInfoLabel.text = $"이어하기: {message}부터";
            else if (saveSystem.HasSave)
                saveInfoLabel.text = message; // 깨진 파일 — 새 게임은 할 수 있다
            else
                saveInfoLabel.text = string.Empty;
        }

        /// <summary>'이어하기': 저장된 날 아침 상점 단계부터.</summary>
        public void ContinueGame()
        {
            if (!IsOpen || !hasSave || GameManager.Instance == null)
                return;
            // 시작 상태를 먼저 만들고 그 위에 저장을 덮는다 (6-1 왕복 테스트와 같은 경로)
            GameManager.Instance.StartNewGame();
            if (!saveSystem.Load(out string message))
                Debug.LogError($"[TitleScreen] 이어하기 실패 - 1일차로 시작: {message}", this);
            Begin();
        }

        /// <summary>'새 게임'. 쓸 수 있는 저장이 있으면 덮어쓰기를 묻는다.</summary>
        public void NewGame()
        {
            if (!IsOpen)
                return;
            if (!hasSave)
            {
                StartFresh();
                return;
            }
            SetMenuVisible(false);
            confirm.Ask($"새 게임을 시작할까요?\n{savedDay}일차까지의 저장이 새 게임 1일차 아침으로 바뀝니다.", "새 게임 시작", StartFresh, () => SetMenuVisible(true));
        }

        private void StartFresh()
        {
            if (!IsOpen || GameManager.Instance == null)
                return;
            GameManager.Instance.StartNewGame();
            saveSystem.RequestSave(); // 1일차 아침 저장 (이전 파일은 .bak)
            Begin();
            // 새 게임만 오프닝 편지 → 닫으면 1일차 상점 (상점은 화면이 비면 열린다)
            if (openingLetter != null)
                openingLetter.Open();
        }

        private void OpenSettings()
        {
            if (!IsOpen)
                return;
            SetMenuVisible(false);
            settings.Open(() => SetMenuVisible(true));
        }

        private void Begin()
        {
            starting = true;
            Close();
        }
    }
}
