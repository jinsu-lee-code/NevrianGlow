using System.Collections.Generic;
using System.Text;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Tutorial;
using NevrianGlowFarm.Systems.Farm;
using NevrianGlowFarm.Systems.Sales;
using NevrianGlowFarm.Systems.Shop;
using NevrianGlowFarm.UI;
using TMPro;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Tutorial
{
    /// <summary>
    /// 3 ~ 6일차 안내 (튜토리얼 v3 5 · 6절, 10/30): 아침마다 편지 한 통 → 오늘의 목표 체크리스트(강제 아님) · 상점 '추천' 꼬리표.
    /// 1 ~ 2일차는 단계형 튜토리얼(TutorialSystem)이 맡고, 튜토리얼이 진행 중인 동안에는 나서지 않는다.
    /// 목표 조건은 지금 상태로 판정한다(Logic GoalConditions) — 행동하면 바로 켜지고, 불러와도 같은 답. 어제 못 한 핵심 목표는 오늘 맨 위에 다시.
    /// 다 체크하면 목표 칸 제목이 금빛으로 한 번 반짝인다 (보상 없음). Core 씬에 하나.
    /// </summary>
    public class DailyGoalSystem : MonoBehaviour, IGoalWorld
    {
        [Header("참조")]
        [Tooltip("일차별 편지 · 목표 · 추천 (Data/Tutorial/DailyGoals)")]
        [SerializeField] private DailyGoalsData data;
        [Tooltip("시작 밭 구획 수 · 구획당 칸 — 확장 밭에 심었는지 (Data/Stats/GameStartSettings)")]
        [SerializeField] private GameStartSettingsData startSettings;
        [Tooltip("편지 화면 (Core UICanvas/OpeningLetterScreen)")]
        [SerializeField] private OpeningLetterScreen letterScreen;
        [Tooltip("목표 칸 (켜고 끔)")]
        [SerializeField] private GameObject panel;
        [Tooltip("목표 칸 제목")]
        [SerializeField] private TMP_Text titleLabel;
        [Tooltip("목표 칸 본문 (한 줄에 하나)")]
        [SerializeField] private TMP_Text bodyLabel;

        [Header("조정값")]
        [Tooltip("목표를 다시 확인하는 간격 (초, 실제 시간)")]
        [SerializeField, Min(0.1f)] private float pollInterval = 0.5f;
        [Tooltip("다 체크했을 때 제목이 금빛으로 반짝이는 시간 (초)")]
        [SerializeField, Min(0f)] private float flashTime = 1.5f;
        [Tooltip("제목 평소 색")]
        [SerializeField] private Color titleColor = new Color(0.85f, 0.95f, 1f, 1f);
        [Tooltip("제목 반짝임 색 (금빛)")]
        [SerializeField] private Color flashColor = new Color(1f, 0.82f, 0.48f, 1f);

        [Header("디버그")]
        [Tooltip("오늘 목표 상태 (읽기 전용)")]
        [SerializeField] private List<string> goalsView = new List<string>();

        private sealed class Entry
        {
            public DailyGoalsData.Goal Goal;
            public bool Done;
            public bool Carried;
        }

        private static DailyGoalSystem instance;
        private readonly List<Entry> today = new List<Entry>();
        private readonly HashSet<string> recommended = new HashSet<string>();
        private int builtDay = -1;
        private string pendingLetter;
        private float pollTimer;
        private float flash = -1f;
        private bool allDoneShown;

        /// <summary>오늘 '추천' 상품인지 (상점 목록 꼬리표).</summary>
        public static bool IsRecommended(string productId) =>
            instance != null && productId != null && instance.isActiveAndEnabled && !TutorialSystem.IsActive && instance.recommended.Contains(productId);

        /// <summary>아침 편지를 아직 못 열었는지 — 상점 화면이 아침에 저절로 열리는 것을 편지 뒤로 미룬다 (v3 2절: 편지 → 상점).</summary>
        public static bool LetterPending => instance != null && instance.isActiveAndEnabled && instance.pendingLetter != null;

        private void Awake()
        {
            if (data == null || startSettings == null || letterScreen == null || panel == null || titleLabel == null || bodyLabel == null)
            {
                Debug.LogError("[DailyGoalSystem] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            instance = this;
            panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void OnEnable() => GameEvents.DayChanged += OnDayChanged;
        private void OnDisable() => GameEvents.DayChanged -= OnDayChanged;

        // 아침: 편지를 먼저(상점 화면은 LetterPending 동안 기다리고, 등대 점등 연출은 화면이 다 닫히기를 기다린다 — 편지 → 상점 · 점등),
        // 목표 · 추천을 새로. 낮에 불러오면(상점 단계가 아니면) 편지는 다시 열지 않는다.
        private void OnDayChanged(int day)
        {
            Build(day);
            var d = data.Get(day);
            var game = GameManager.Instance;
            bool morning = game != null && game.Phase == Logic.Game.DayPhase.Shop;
            pendingLetter = d != null && !string.IsNullOrEmpty(d.LetterId) && morning ? d.LetterId : null;
            TryOpenLetter();
        }

        private void TryOpenLetter()
        {
            if (pendingLetter == null)
                return;
            if (TutorialSystem.IsActive || (GameScreen.Current != null && GameScreen.Current != letterScreen))
                return; // 튜토리얼이 끝나거나 다른 화면(정산이 닫히는 중 · 시작 연출)이 비키면 다시
            if (letterScreen.Open(pendingLetter))
                pendingLetter = null;
        }

        private void Build(int day)
        {
            builtDay = day;
            today.Clear();
            recommended.Clear();
            allDoneShown = false;
            flash = -1f;
            // 어제 못 한 핵심 목표를 맨 위에 (지금 상태로 다시 판정 — 그사이 했으면 빠진다)
            var yesterday = data.Get(day - 1);
            if (yesterday != null)
            {
                bool carried = false;
                foreach (var g in yesterday.Goals)
                    if (g != null && g.Core && !GoalConditions.IsMet(g.Condition, this))
                    {
                        today.Add(new Entry { Goal = g, Carried = true });
                        carried = true;
                    }
                if (carried)
                    foreach (var id in yesterday.Recommended)
                        recommended.Add(id);
            }
            var d = data.Get(day);
            if (d != null)
            {
                foreach (var g in d.Goals)
                    if (g != null && !today.Exists(e => e.Goal.Id == g.Id))
                        today.Add(new Entry { Goal = g });
                foreach (var id in d.Recommended)
                    recommended.Add(id);
            }
            Refresh(force: true);
        }

        private void Update()
        {
            var game = GameManager.Instance;
            bool show = game != null && game.IsStarted && today.Count > 0 && !TutorialSystem.IsActive && GameScreen.Current == null;
            if (game != null && game.IsStarted && builtDay != game.Day)
                Build(game.Day);
            if (pendingLetter != null)
                TryOpenLetter();
            if (panel.activeSelf != show)
                panel.SetActive(show);
            pollTimer -= Time.unscaledDeltaTime;
            if (pollTimer <= 0f)
            {
                pollTimer = pollInterval;
                Refresh(force: false);
            }
            if (flash >= 0f)
            {
                flash += Time.unscaledDeltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(flash / Mathf.Max(0.01f, flashTime)) * Mathf.PI);
                titleLabel.color = Color.Lerp(titleColor, flashColor, k);
                if (flash >= flashTime)
                {
                    flash = -1f;
                    titleLabel.color = titleColor;
                }
            }
        }

        private void Refresh(bool force)
        {
            bool changed = force;
            foreach (var e in today)
                if (!e.Done && GoalConditions.IsMet(e.Goal.Condition, this))
                {
                    e.Done = true; // 그날은 켜진 채 (판 작물 등으로 상태가 돌아가도)
                    changed = true;
                }
            if (!changed)
                return;
            var text = new StringBuilder();
            goalsView.Clear();
            bool all = today.Count > 0;
            foreach (var e in today)
            {
                // 게임 화면 글자 — 체크 기호 대신 색 · 가운데 줄 (글꼴에 없는 기호를 쓰지 않음)
                text.AppendLine(e.Done ? $"<color=#7DF29B><s>{e.Goal.Text}</s></color>" : $"· {(e.Carried ? "<color=#FFD27A>" + e.Goal.Text + "</color>" : e.Goal.Text)}");
                goalsView.Add($"{(e.Done ? "완료" : "  ")} {(e.Carried ? "(어제) " : string.Empty)}{e.Goal.Text} [{e.Goal.Condition}]");
                all &= e.Done;
            }
            titleLabel.text = "오늘의 목표";
            bodyLabel.text = text.ToString().TrimEnd();
            if (all && !allDoneShown)
            {
                allDoneShown = true;
                flash = 0f;
            }
        }

        // ── 목표 조건이 묻는 상태 (IGoalWorld) ──

        public int TiersBought(string productId) => ShopSystem.Instance != null ? ShopSystem.Instance.State.TiersBought(productId) : 0;

        private FarmGrid farm;
        private FarmGrid Farm => farm != null ? farm : farm = FindFirstObjectByType<FarmGrid>();

        public int PlantedCells
        {
            get
            {
                int n = 0;
                if (Farm != null && Farm.State != null)
                    foreach (var c in Farm.State.Cells)
                        if (c.HasCrop)
                            n++;
                return n;
            }
        }

        public bool HasPlanted(string cropId)
        {
            if (Farm != null && Farm.State != null)
                foreach (var c in Farm.State.Cells)
                    if (c.CropId == cropId)
                        return true;
            return false;
        }

        public bool HasFertilized(string cropId)
        {
            if (Farm != null && Farm.State != null)
                foreach (var c in Farm.State.Cells)
                    if (c.CropId == cropId && c.IsFertilized)
                        return true;
            return false;
        }

        public bool SitePlanted
        {
            get
            {
                if (Farm == null || Farm.State == null)
                    return false;
                var cells = Farm.State.Cells;
                for (int i = startSettings.StartPlotCount * startSettings.CellsPerPlot; i < cells.Count; i++)
                    if (cells[i].HasCrop)
                        return true;
                return false;
            }
        }

        public bool HasHarvested(string cropId) => GameManager.Instance != null && GameManager.Instance.HasHarvested(cropId);

        public bool SalesDoneToday =>
            SalesSystem.Instance != null && SalesSystem.Instance.LastResult.HasValue && GameManager.Instance != null
            && SalesSystem.Instance.LastResult.Value.Day == GameManager.Instance.Day;
    }
}
