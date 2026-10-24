using System;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Tutorial;
using NevrianGlowFarm.Systems.Player;
using NevrianGlowFarm.UI;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Tutorial
{
    /// <summary>
    /// 튜토리얼 어댑터 (TASKS 5-7). 규칙은 Logic TutorialProgress, 여기는 GameEvents(단계 · 개간 · 심기 · 물 · 수확 · 버리기 · 닦기 · 진열 · 핫바 · 편지 · 지역 · 장사 끝)를
    /// 기획 CSV의 완료 이벤트 이름으로 바꿔 넘기고, 잠금 질문(IsLocked)에 답하고, 단계에 맞춰 편지 · 스킬 화면을 연다. Core 씬에 하나.
    /// 새 게임에서만 시작(GameEvents.NewGameStarted), 저장에는 현재 단계 id(SaveSnapshot.TutorialStep), 이어하기는 그 단계부터.
    /// 특정 단계 id로 분기하지 않는다 — 단계가 무엇을 기다리는지는 완료 이벤트 종류로만 본다.
    /// 화면 위쪽 한 줄은 TutorialHudView, 잠금은 각 입구(문 · 간판 · 상점 · 스킬)가 묻는다.
    /// </summary>
    [DefaultExecutionOrder(-60)] // 상점 화면(하루 시작 자동 열기)보다 먼저 Update — 편지가 먼저 열리게
    public class TutorialSystem : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("단계 · 편지 데이터 (Data/Tutorial/TutorialSteps)")]
        [SerializeField] private TutorialStepsData data;
        [Tooltip("편지 화면 (Core UICanvas/OpeningLetterScreen) — 완료 이벤트가 letter_closed인 단계에 들어서면 그 편지를 연다. 비우면 편지는 열지 않음")]
        [SerializeField] private OpeningLetterScreen letterScreen;

        [Header("조정값")]
        [Tooltip("한 프레임에 이보다 멀리 움직이면 순간이동으로 보고 걸은 거리에 더하지 않는다 (m)")]
        [SerializeField, Min(0.5f)] private float teleportThreshold = 5f;

        [Header("디버그")]
        [Tooltip("상태 · 현재 단계 (읽기 전용)")]
        [SerializeField] private string currentView;
        [Tooltip("현재 단계 진행 (읽기 전용)")]
        [SerializeField] private string progressView;

        private static TutorialSystem instance;
        private TutorialProgress progress;
        private PlayerInventory inventory;
        private Transform player;
        private Vector3 lastPlayerPosition;
        private bool hasLastPosition;
        private string pendingLetter;
        private bool pendingSkill;
        private bool pushing;

        public static TutorialSystem Instance => instance;

        /// <summary>진행기 (디버그 창).</summary>
        public TutorialProgress Progress => progress;
        public TutorialStep Current => progress?.Current;
        public TutorialStatus Status => progress != null ? progress.Status : TutorialStatus.Off;
        public bool IsRunning => progress != null && progress.IsRunning;

        /// <summary>단계가 바뀌었을 때 (새 단계 — 끝났거나 꺼졌으면 null). HUD가 받는다.</summary>
        public event Action<TutorialStep> StepChanged;

        /// <summary>튜토리얼이 진행 중인지 (없거나 끝났으면 false) — 튜토리얼 고정값(손님 수 · 모두 구매 · 무작위 끔 · 발자국)을 쓰는 조건.</summary>
        public static bool IsActive => instance != null && instance.progress != null && instance.progress.IsRunning;

        /// <summary>현재 단계가 그 이름을 잠갔는지 (튜토리얼이 없거나 끝났으면 false).</summary>
        public static bool IsLocked(string lockName) => instance != null && instance.progress != null && instance.progress.IsLocked(lockName);

        /// <summary>그 이름이 잠겨 있고 targetId가 안내한 대상이 아닌지 (buy_other · other_skills).</summary>
        public static bool IsLockedFor(string lockName, string targetId) =>
            instance != null && instance.progress != null && instance.progress.IsLockedFor(lockName, targetId);

        /// <summary>
        /// 오늘 튜토리얼 단계들이 그 종류로 요구하는 양 (없거나 진행 중이 아니면 0). 가게 정리(얼룩 · 쓰레기) · 장사가 그만큼은 생기게 보장한다
        /// (기획 튜토리얼 고정값 — 2일차 발자국 2, 레벨업 보장).
        /// </summary>
        public static float RequiredToday(string kind)
        {
            var game = GameManager.Instance;
            if (instance == null || instance.progress == null || game == null || !game.IsStarted)
                return 0f;
            return instance.progress.RequiredFor(kind, game.Day);
        }

        /// <summary>잠긴 입구가 보여 줄 한 줄 — 지금 할 일 (게임 화면 글자).</summary>
        public static string LockedHint
        {
            get
            {
                var step = instance != null ? instance.Current : null;
                return step != null && !string.IsNullOrEmpty(step.Text) ? step.Text : "지금 할 일을 먼저 해요";
            }
        }

        private void Awake()
        {
            if (data == null)
            {
                Debug.LogError("[TutorialSystem] 참조가 비어 있음 - 인스펙터의 참조 항목 확인 (Glow Farm/Setup/Create Tutorial HUD)", this);
                enabled = false;
                return;
            }
            if (instance != null && instance != this)
            {
                Debug.LogError("[TutorialSystem] 튜토리얼이 두 개 있음 - Core 씬에 하나만 둔다", this);
                enabled = false;
                return;
            }
            instance = this;
            progress = new TutorialProgress(data.ToSteps());
            progress.StepChanged += OnStepChanged;
            progress.Finished += () => Debug.Log("[Tutorial] 튜토리얼 끝 - 안내 꺼짐 · 잠금 없음");
            UpdateDebugView();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
            if (inventory != null)
                inventory.Changed -= OnInventoryChanged;
        }

        private void OnEnable()
        {
            if (!enabled)
                return;
            GameEvents.NewGameStarted += OnNewGame;
            GameEvents.PhaseChanged += OnPhaseChanged;
            GameEvents.LeveledUp += OnLeveledUp;
            GameEvents.SkillLevelChanged += OnSkillLevelChanged;
            GameEvents.CellTilled += OnCellTilled;
            GameEvents.CropPlanted += OnCropPlanted;
            GameEvents.CellsWatered += OnCellsWatered;
            GameEvents.CropHarvested += OnCropHarvested;
            GameEvents.TrashDisposed += OnTrashDisposed;
            GameEvents.StainsCleared += OnStainsCleared;
            GameEvents.ItemDisplayed += OnItemDisplayed;
            GameEvents.HotbarSelected += OnHotbarSelected;
            GameEvents.LetterClosed += OnLetterClosed;
            GameEvents.ZoneArrived += OnZoneArrived;
            GameEvents.SalesFinished += OnSalesFinished;
            OpeningLetterScreen.LetterSource = FindLetter;
        }

        private void OnDisable()
        {
            GameEvents.NewGameStarted -= OnNewGame;
            GameEvents.PhaseChanged -= OnPhaseChanged;
            GameEvents.LeveledUp -= OnLeveledUp;
            GameEvents.SkillLevelChanged -= OnSkillLevelChanged;
            GameEvents.CellTilled -= OnCellTilled;
            GameEvents.CropPlanted -= OnCropPlanted;
            GameEvents.CellsWatered -= OnCellsWatered;
            GameEvents.CropHarvested -= OnCropHarvested;
            GameEvents.TrashDisposed -= OnTrashDisposed;
            GameEvents.StainsCleared -= OnStainsCleared;
            GameEvents.ItemDisplayed -= OnItemDisplayed;
            GameEvents.HotbarSelected -= OnHotbarSelected;
            GameEvents.LetterClosed -= OnLetterClosed;
            GameEvents.ZoneArrived -= OnZoneArrived;
            GameEvents.SalesFinished -= OnSalesFinished;
            if (OpeningLetterScreen.LetterSource == (Func<string, (string, string)?>)FindLetter)
                OpeningLetterScreen.LetterSource = null;
        }

        private void Start()
        {
            // 인벤토리 · 플레이어는 Core 씬 — 시작할 때 한 번만 찾는다 (프레임마다 Find 금지)
            inventory = PlayerInventory.Instance;
            if (inventory != null)
                inventory.Changed += OnInventoryChanged;
            var controller = FindFirstObjectByType<PlayerController>();
            player = controller != null ? controller.transform : null;
        }

        // ── GameEvents → 완료 이벤트 이름 ──

        private void OnNewGame()
        {
            var game = GameManager.Instance;
            progress.Start(game != null && game.IsStarted ? game.Day : DayCycle.FirstDay);
        }

        private void OnPhaseChanged(PhaseChange change)
        {
            if (!progress.IsRunning)
                return;
            if (change.DayChanged)
                progress.BeginDay(change.Day); // = next_day 이벤트 + 하루 누적 비움 + 지난 날 단계 건너뜀
            if (change.DayChanged || change.PreviousPhase != change.Phase)
                progress.Report(TutorialEvents.Phase, TutorialPhaseNames.Of(change.Phase));
        }

        private void OnLeveledUp(int level, int levelsGained) => progress.Report(TutorialEvents.LeveledUp, null, Mathf.Max(1, levelsGained));
        private void OnSkillLevelChanged(string skillId, int level) { if (level > 0) progress.Report(TutorialEvents.Skill, skillId); }
        private void OnCellTilled(int cellIndex) => progress.Report(TutorialEvents.Tilled);
        private void OnCropPlanted(string cropId) => progress.Report(TutorialEvents.Planted, cropId);
        private void OnCellsWatered(int count) => progress.Report(TutorialEvents.Watered, null, count);
        private void OnCropHarvested(string cropId, int count) => progress.Report(TutorialEvents.Harvested, cropId, count);
        private void OnTrashDisposed(int count) => progress.Report(TutorialEvents.Trashed, null, count);
        private void OnStainsCleared(int count) => progress.Report(TutorialEvents.StainsCleared, null, count);
        private void OnItemDisplayed(string itemId) => progress.Report(TutorialEvents.Displayed, itemId);
        private void OnHotbarSelected(string itemId) { if (!string.IsNullOrEmpty(itemId)) progress.Report(TutorialEvents.Selected, itemId); }
        private void OnLetterClosed(string letterId) => progress.Report(TutorialEvents.LetterClosed, letterId);
        private void OnZoneArrived(string zoneId) => progress.Report(TutorialEvents.Zone, zoneId);
        private void OnSalesFinished() => progress.Report(TutorialEvents.SalesDone);
        private void OnInventoryChanged() => PushOwned();

        // "가진 개수" 조건은 누적이 아니라 지금 값 — 단계에 들어설 때와 인벤토리가 바뀔 때 그 아이템 개수를 넣는다
        private void PushOwned()
        {
            var step = progress.Current;
            if (step == null || step.Condition.Kind != TutorialEvents.Owned || inventory == null)
                return;
            var item = inventory.Lookup(step.Condition.Target);
            progress.SetState(TutorialEvents.Owned, step.Condition.Target, item != null ? inventory.CountOf(item) : 0);
        }

        // ── 단계 전환 ──

        private void OnStepChanged(TutorialStep step)
        {
            pendingLetter = null;
            pendingSkill = false;
            if (step != null)
            {
                // 단계가 기다리는 것에 맞춰 화면을 연다 (편지 · 스킬 트리). id가 아니라 완료 이벤트 종류로 정한다
                if (step.Condition.Kind == TutorialEvents.LetterClosed && letterScreen != null && data.TryGetLetter(step.Condition.Target, out _))
                    pendingLetter = step.Condition.Target;
                else if (step.Condition.Kind == TutorialEvents.Skill)
                    pendingSkill = true;
            }
            UpdateDebugView();
            StepChanged?.Invoke(step);
            if (!pushing)
            {
                // 들어선 단계가 '가진 개수'면 지금 값으로 바로 본다 — 넘어가면 그 다음 단계도 (중첩 호출은 건너뜀)
                pushing = true;
                for (int i = 0; i < 8; i++)
                {
                    var before = progress.Current;
                    PushOwned();
                    if (progress.Current == before)
                        break;
                }
                pushing = false;
            }
            TryOpenPending();
        }

        private void TryOpenPending()
        {
            if (pendingLetter != null && letterScreen != null)
            {
                if (letterScreen.IsOpen && letterScreen.CurrentLetterId == pendingLetter)
                    pendingLetter = null;
                else if (CanOpenScreen() && letterScreen.Open(pendingLetter))
                    pendingLetter = null;
            }
            // 스킬 트리는 그 단계가 끝날 때까지 열려 있어야 한다 (close_skill 잠금) — 정산 화면이 먼저 뜨며 닫았으면 그 위에 다시 연다
            if (pendingSkill)
            {
                var skill = SkillScreen.Instance;
                if (skill == null)
                    pendingSkill = false;
                else if (!skill.IsOpen && CanOpenScreen())
                    skill.Open();
            }
        }

        // 화면이 비어 있으면 연다. 정산 화면은 잠깐 비켜 준다(닫히면 다시 뜬다 — 정산이 열릴 차례면 먼저 열리게 기다렸다가 그 위에 연다).
        // 스킬 트리 위에는 바로 연다(배우자마자 편지). 다른 화면(타이틀 · 상점 …)이 열려 있으면 닫힐 때까지 기다린다
        private static bool CanOpenScreen()
        {
            var current = GameScreen.Current;
            var settlement = SettlementScreen.Instance;
            if (current == null)
                return settlement == null || !settlement.IsPending;
            if (current == settlement)
            {
                settlement.CloseTemporarily();
                return GameScreen.Current == null;
            }
            return current is SkillScreen;
        }

        private void Update()
        {
            if (progress == null || !progress.IsRunning)
            {
                hasLastPosition = false;
                return;
            }
            if (pendingLetter != null || (pendingSkill && SkillScreen.Instance != null && !SkillScreen.Instance.IsOpen))
                TryOpenPending();
            TrackMovement();
        }

        // 걸은 거리: 조건이 moved인 동안만 수평 이동량을 더한다 (순간이동 · 장소 이동은 뺌)
        private void TrackMovement()
        {
            var step = progress.Current;
            if (step == null || step.Condition.Kind != TutorialEvents.Moved || player == null)
            {
                hasLastPosition = false;
                return;
            }
            Vector3 p = player.position;
            if (hasLastPosition)
            {
                Vector3 d = p - lastPlayerPosition;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > 0.0001f && dist < teleportThreshold)
                {
                    progress.Report(TutorialEvents.Moved, null, dist);
                    UpdateDebugView();
                }
            }
            lastPlayerPosition = p;
            hasLastPosition = true;
        }

        private (string Title, string Body)? FindLetter(string id) =>
            data != null && data.TryGetLetter(id, out var letter) ? (letter.Title, letter.Body) : ((string, string)?)null;

        // ── 저장 (6-1) ──

        public void CaptureTo(Logic.Save.SaveSnapshot into) => into.TutorialStep = progress.SaveKey;

        /// <summary>저장 내용의 단계로 되돌린다 (null = 튜토리얼 없던 저장 → 끔). 모르는 단계 id면 끝낸 것으로.</summary>
        public void LoadFrom(Logic.Save.SaveSnapshot from)
        {
            if (!progress.Restore(from.TutorialStep, from.Day))
                Debug.LogWarning($"[TutorialSystem] 저장된 튜토리얼 단계 '{from.TutorialStep}'가 데이터에 없음 - 튜토리얼을 끝낸 것으로 봄", this);
            UpdateDebugView();
        }

        // ── 디버그 (Glow Farm 디버그 창) ──

        public void DebugSkip() { progress.Skip(); UpdateDebugView(); }
        public void DebugTurnOff() { progress.TurnOff(); UpdateDebugView(); }
        public void DebugRestart() { var game = GameManager.Instance; progress.Start(game != null && game.IsStarted ? game.Day : DayCycle.FirstDay); UpdateDebugView(); }

        private void UpdateDebugView()
        {
            if (progress == null)
                return;
            var step = progress.Current;
            currentView = step == null ? progress.Status.ToString() : $"{step.Id} ({step.Day}일차 {step.Phase}) {step.Text}";
            progressView = step == null ? string.Empty : $"{step.CompleteEvent}: {progress.CurrentProgress:0.##} / {step.Condition.Threshold:0.##} · 잠금 {step.LocksText}";
        }
    }
}
