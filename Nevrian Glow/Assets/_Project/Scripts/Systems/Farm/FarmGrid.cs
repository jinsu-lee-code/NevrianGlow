using System.Collections;
using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Farm;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Farm
{
    /// <summary>
    /// 농장 밭 (GDD 4-2, 노션 GDD 12.3). 이 오브젝트 위치 = 첫 구획 첫 밭의 중심(BALANCE 9-1 밭 원점).
    /// 칸 상태 · 규칙은 Logic(FarmState · FarmRules · NightPipeline), 여기는 칸 표시 생성 · 입력 전달 · 밤 처리 연결만 한다.
    /// 밤 처리는 일차가 바뀌는 단계 전환(PhaseChange.DayChanged) 때 1회.
    /// </summary>
    public class FarmGrid : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("구획 수 · 칸 간격 · 개간 작업량 (Data/Stats/GameStartSettings)")]
        [SerializeField] private GameStartSettingsData settings;
        [Tooltip("작물 id → 작물 데이터 (Data/ContentDatabase)")]
        [SerializeField] private ContentDatabase database;
        [Tooltip("칸 프리팹 (Prefabs/Farm/farm_cell)")]
        [SerializeField] private FarmCellView cellPrefab;
        [Tooltip("밭(2×2칸) 테두리 — 밭마다 4칸 가운데에 하나 (아트 pf_env_plot, 비우면 없음)")]
        [SerializeField] private GameObject bedPrefab;
        [Tooltip("겹치면 안 되는 구역 (길 · 문 주변) — 겹치면 기즈모 빨강 + 시작 시 경고")]
        [SerializeField] private List<ForbiddenZone> forbiddenZones = new List<ForbiddenZone>();
        [Tooltip("성장일 능력치 — 작물 첫 성장일에 더함 (Data/Stats/stat_grow_days, 비우면 그대로)")]
        [SerializeField] private StatData growDaysStat;

        [Header("조정값")]
        [Tooltip("칸 하나의 흙 크기 — 겹침 검사에도 쓴다, 칸 프리팹 크기와 같게 (m)")]
        [SerializeField, Min(0.1f)] private float cellFootprint = 1.6f;
        [Tooltip("미개간 흙 색 (BALANCE 9-5 임시)")]
        [SerializeField] private Color untilledColor = new Color(0.46f, 0.45f, 0.48f); // 아트 칸 흙(흰 바탕) · 밤 조명에서 검게 보이지 않게 밝힘 (2026-10-23, 사용자)
        [Tooltip("개간된 흙 색 — 개간 중에는 진행만큼 이 색에 가까워진다")]
        [SerializeField] private Color tilledColor = new Color(0.17f, 0.33f, 0.47f);
        [Tooltip("젖은 흙 색 (심은 칸에 물을 줬을 때)")]
        [SerializeField] private Color wetColor = new Color(0.08f, 0.22f, 0.42f);
        [Tooltip("비료를 준 칸의 흙 색 (BALANCE 9-5 임시 — 보라 발광 쪽)")]
        [SerializeField] private Color fertilizedColor = new Color(0.63f, 0.55f, 1f);
        [Tooltip("비료 색이 흙 색에 섞이는 정도 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float fertilizedTint = 0.35f;
        [Tooltip("갓 심은 작물 크기 (배)")]
        [SerializeField, Range(0.05f, 1f)] private float seedlingScale = 0.25f;
        [Tooltip("다 자란 작물 크기 (배)")]
        [SerializeField, Range(0.1f, 2f)] private float grownScale = 0.9f;
        [Tooltip("다 자란 작물을 흰색 쪽으로 밝히는 정도 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float readyBrighten = 0.35f;

        [Header("기즈모")]
        [Tooltip("칸 상태 색 (파랑 = 개간, 초록 = 자라는 중, 노랑 = 다 자람, 빨강 = 금지 구역 겹침)")]
        [SerializeField] private bool drawCells = true;
        [Tooltip("다음 구획 자리 빨강 점선")]
        [SerializeField] private bool drawNextPlot = true;

        [Header("디버그")]
        [Tooltip("칸 상태 요약 (읽기 전용)")]
        [SerializeField] private string summary;

        private FarmState state;
        private readonly NightPipeline night = new NightPipeline();
        private readonly List<FarmCellView> views = new List<FarmCellView>();
        private Hotbar hotbar;
        private float busyUntil;
        private Transform viewer;
        private int aimIndex = -1;
        private PairDirection aimPair;

        public FarmState State => state;
        public int CellCount => state != null ? state.Cells.Count : 0;
        public IReadOnlyList<ForbiddenZone> ForbiddenZones => forbiddenZones;
        public string Summary => summary;

        private void Awake()
        {
            if (settings == null || cellPrefab == null || database == null)
            {
                Debug.LogError("[FarmGrid] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            state = new FarmState(settings.CellsPerPlot, settings.StartPlotCount, SpecOf);
            WarnForbiddenOverlaps();
        }

        private void OnEnable()
        {
            GameEvents.PhaseChanged += OnPhaseChanged;
            GameEvents.StatsChanged += OnStatsChanged;
        }

        private void OnDisable()
        {
            GameEvents.PhaseChanged -= OnPhaseChanged;
            GameEvents.StatsChanged -= OnStatsChanged;
        }

        // 성장일이 줄면 이미 자란 칸이 바로 다 자람이 될 수 있다 → 겉모습만 다시 (성장 처리는 밤에만)
        private void OnStatsChanged()
        {
            if (state != null && views.Count > 0)
                RefreshAll();
        }

        private void Start()
        {
            if (!enabled)
                return;
            // 핫바는 Core 씬에 있어 씬 사이 참조를 걸 수 없다 → 시작할 때 한 번만 찾는다.
            hotbar = FindFirstObjectByType<Hotbar>();
            if (hotbar == null)
                Debug.LogError("[FarmGrid] Hotbar를 찾지 못함 — Core 씬에서 플레이", this);
            // 물뿌리개 2칸 방향 기준 = 1인칭 카메라 (Core 씬)
            var cam = Camera.main;
            viewer = cam != null ? cam.transform : null;
            SpawnMissingViews();
            RefreshAll();
        }

        // ── 입력 (FarmCellView가 전달) ─────────────────────────────

        public string GetUsePrompt(int index, ItemData held)
        {
            if (state == null || held == null)
                return null;
            var cell = state.Cells[index];
            var status = state.StatusOf(index);

            if (held is HoeData hoe && (status == FarmCellStatus.Untilled || status == FarmCellStatus.Tilling))
            {
                int total = Mathf.CeilToInt(settings.TillWorkRequired / hoe.WorkPerUse - 1e-4f);
                int done = Mathf.Min(total, Mathf.RoundToInt(cell.TillProgress / hoe.WorkPerUse));
                return $"{hoe.DisplayName}(으)로 갈기 ({done}/{total})";
            }
            if (held is SeedData seed && seed.Crop != null && status == FarmCellStatus.Tilled && hotbar != null)
                return $"{seed.Crop.DisplayName} 심기 (씨앗 {hotbar.SelectedCount}개)";
            if (held is FertilizerData fertilizer && cell.HasCrop)
            {
                // 게임 화면 글자 — "—" 대신 "-" (글꼴에 없음)
                if (status == FarmCellStatus.Ready)
                    return null;
                if (cell.IsFertilized)
                    return "비료 - 이미 줬음 (작물당 1번)";
                int have = hotbar != null ? hotbar.SelectedCount : 0;
                return $"{held.DisplayName} 주기 - 성장 {fertilizer.GrowDaysReduction}일 단축, 내일부터 ({have}개)";
            }
            if (held is WateringCanData can)
            {
                // 범위(등급 칸 수) 안에서 물이 필요한 칸이 하나라도 있으면 (조준한 칸이 아니어도)
                int thirsty = CountThirsty(index, can, PairFor(index));
                if (thirsty == 0)
                    return null;
                return can.CellCount > 1 ? $"물주기 ({thirsty}칸)" : "물주기";
            }
            return null;
        }

        public void Use(int index, ItemData held)
        {
            if (state == null || held == null || Time.time < busyUntil)
                return;

            // 동작이 있는 도구(물뿌리개 기울이기 0.3초)는 동작이 끝난 뒤 효과 — 그동안은 다시 쓰지 않는다.
            // 2칸 짝 방향은 누른 순간 보던 방향으로 정한다 (기울이는 동안 고개를 돌려도 바뀌지 않음).
            var pair = PairFor(index);
            if (held is ToolData tool && tool.UseDelay > 0f)
            {
                busyUntil = Time.time + tool.UseDelay;
                StartCoroutine(UseAfter(index, held, pair, tool.UseDelay));
                return;
            }
            UseNow(index, held, pair);
        }

        private IEnumerator UseAfter(int index, ItemData held, PairDirection pair, float delay)
        {
            yield return new WaitForSeconds(delay);
            UseNow(index, held, pair);
        }

        /// <summary>
        /// 조준점이 움직일 때(InteractionRaycaster → FarmCellView) — 2칸 짝 방향이 바뀌면 true(프롬프트 칸 수 다시 받기).
        /// </summary>
        public bool UpdateAim(int index, ItemData held)
        {
            if (!(held is WateringCanData) || state == null)
                return false;
            var pair = PairFor(index);
            if (index == aimIndex && pair == aimPair)
                return false;
            aimIndex = index;
            aimPair = pair;
            return true;
        }

        public void ClearAim() => aimIndex = -1;

        /// <summary>
        /// 같은 밭의 두 옆 칸(i^1 · i^2) 중 플레이어가 보는 수평 방향에 더 가까운 쪽. 칸의 실제 위치로 고르므로 밭 배치 방향에 기대지 않는다.
        /// </summary>
        private PairDirection PairFor(int index)
        {
            int along = index ^ 1, across = index ^ 2;
            if (viewer == null || across >= views.Count || along >= views.Count)
                return PairDirection.Along;
            Vector3 look = Vector3.ProjectOnPlane(viewer.forward, Vector3.up);
            Vector3 a = Vector3.ProjectOnPlane(views[along].transform.position - views[index].transform.position, Vector3.up).normalized;
            Vector3 b = Vector3.ProjectOnPlane(views[across].transform.position - views[index].transform.position, Vector3.up).normalized;
            return Mathf.Abs(Vector3.Dot(look, a)) >= Mathf.Abs(Vector3.Dot(look, b)) ? PairDirection.Along : PairDirection.Across;
        }

        private void UseNow(int index, ItemData held, PairDirection pair)
        {
            var cell = state.Cells[index];
            bool changed = false;

            if (held is WateringCanData can)
            {
                int watered = 0;
                foreach (int i in WateringPattern.Cells(index, can.CellCount, state.Cells.Count, pair))
                {
                    var c = state.Cells[i];
                    if (c.HasCrop && FarmRules.TryWater(c, SpecOf(c.CropId)))
                    {
                        RefreshCell(i, false);
                        views[i].Splash();
                        watered++;
                    }
                }
                if (watered > 0)
                    UpdateSummary();
                AudioManager.Play(SoundCues.WaterPour, CellPosition(index)); // 기울인 뒤 물이 나오는 순간 (마른 땅이어도 붓는 소리)
                return;
            }

            if (held is HoeData hoe)
                changed = FarmRules.TryTill(cell, hoe.WorkPerUse, settings.TillWorkRequired);
            else if (held is SeedData seed && seed.Crop != null && FarmRules.GetStatus(cell, default) == FarmCellStatus.Tilled)
            {
                // 씨앗을 먼저 쓰고 심는다 — 씨앗이 없으면 심지 않는다. 데이터베이스에 없는 작물은 심지 않는다(성장 규칙을 찾을 수 없음).
                if (!database.TryGet(seed.Crop.Id, out CropData _))
                    Debug.LogError($"[FarmGrid] 작물 '{seed.Crop.Id}'가 ContentDatabase에 없음 — Glow Farm/Data/Refresh Content Database", this);
                else if (hotbar != null && hotbar.TryConsumeSelected(1))
                {
                    changed = FarmRules.TryPlant(cell, seed.Crop.Id);
                    if (changed)
                        AudioManager.Play(SoundCues.SeedPlant, CellPosition(index));
                }
            }

            else if (held is FertilizerData fertilizer && cell.HasCrop && FarmRules.CanFertilize(cell, SpecOf(cell.CropId)))
            {
                // 비료를 먼저 쓰고 준다 — 비료가 없으면 주지 않는다
                if (hotbar != null && hotbar.TryConsumeSelected(1))
                {
                    changed = FarmRules.TryFertilize(cell, SpecOf(cell.CropId), fertilizer.GrowDaysReduction);
                    if (changed)
                        AudioManager.Play(SoundCues.FertilizerSparkle, CellPosition(index));
                }
            }

            if (changed)
            {
                // 흙 연출(아트 SoilFX): 괭이질 한 번 = 판 자리가 열림, 심기 = 구덩이 → 흙 덮기
                RefreshCell(index, true, tillHit: held is HoeData);
                if (held is SeedData)
                    views[index].Planted();
            }
        }

        /// <summary>물뿌리개 범위 안에서 물을 주면 바뀌는 칸 수.</summary>
        private int CountThirsty(int index, WateringCanData can, PairDirection pair)
        {
            int count = 0;
            foreach (int i in WateringPattern.Cells(index, can.CellCount, state.Cells.Count, pair))
            {
                var c = state.Cells[i];
                if (c.HasCrop && FarmRules.NeedsWatering(c, SpecOf(c.CropId)))
                    count++;
            }
            return count;
        }

        public string GetHandPrompt(int index)
        {
            if (state == null || state.StatusOf(index) != FarmCellStatus.Ready)
                return null;
            var cell = state.Cells[index];
            var crop = CropOf(cell.CropId);
            int yield = SpecOf(cell.CropId).HarvestYield;
            return hotbar != null && hotbar.CanAdd(crop, yield)
                ? $"{crop.DisplayName} 수확 ×{yield}"
                : $"{crop.DisplayName} 수확 불가 - 넣을 칸 없음";
        }

        public void InteractHand(int index)
        {
            if (state == null || hotbar == null || state.StatusOf(index) != FarmCellStatus.Ready)
                return;
            var cell = state.Cells[index];
            var crop = CropOf(cell.CropId);
            var spec = SpecOf(cell.CropId);
            // 넣을 자리가 없으면 수확하지 않는다 (GDD 4-2).
            if (!hotbar.CanAdd(crop, spec.HarvestYield))
                return;
            var result = FarmRules.TryHarvest(cell, spec);
            if (result.Succeeded)
            {
                hotbar.TryAdd(crop, result.Yield);
                views[index].HarvestBurst(); // 모델이 바뀌기 전 자리에서 반짝임
                RefreshCell(index);
                AudioManager.Play(SoundCues.HarvestPull, CellPosition(index));
                if (GameManager.Instance != null)
                    GameManager.Instance.RecordHarvest(crop.Id); // 해금 조건 "첫 수확"
            }
        }

        // ── 밤 처리 ───────────────────────────────────────────

        private void OnPhaseChanged(PhaseChange change)
        {
            if (change.DayChanged)
                RunNight();
        }

        /// <summary>밤 처리 1회 (성장 → … → 작물 없는 칸 미개간). 일차 전환 때 자동으로 불린다.</summary>
        public void RunNight()
        {
            if (state == null)
                return;
            int readyBefore = CountReady();
            night.Run(state);
            RefreshAll();
            if (CountReady() > readyBefore)
                AudioManager.Play(SoundCues.CropMature); // 아침에 다 자란 작물이 생겼으면 1회
        }

        private int CountReady()
        {
            int count = 0;
            for (int i = 0; i < state.Cells.Count; i++)
                if (state.StatusOf(i) == FarmCellStatus.Ready)
                    count++;
            return count;
        }

        // ── 저장 (6-1) ──

        public void CaptureTo(Logic.Save.SaveSnapshot into)
        {
            if (state != null)
                Logic.Save.SaveState.CaptureFarm(state, into);
        }

        /// <summary>저장 내용으로 칸을 바꾼다 (구획 수 늘리기 + 칸 값). 밤 처리는 하지 않는다.</summary>
        public void LoadFrom(Logic.Save.SaveSnapshot from)
        {
            if (state == null)
                return;
            int plots = Mathf.Clamp(from.PlotCount, state.PlotCount, settings.MaxPlotCount);
            if (plots != from.PlotCount)
                Debug.LogWarning($"[FarmGrid] 저장된 구획 {from.PlotCount}개를 {plots}개로 맞춤 (현재 {state.PlotCount} ~ 최대 {settings.MaxPlotCount})", this);
            from.PlotCount = plots;
            Logic.Save.SaveState.ApplyFarm(state, from);
            SpawnMissingViews();
            RefreshAll();
        }

        /// <summary>디버그: 자라는 칸에 모두 물을 준다. 바뀐 칸 수를 돌려준다.</summary>
        public int DebugWaterAll()
        {
            if (state == null)
                return 0;
            int count = 0;
            for (int i = 0; i < state.Cells.Count; i++)
            {
                var cell = state.Cells[i];
                if (cell.HasCrop && FarmRules.TryWater(cell, SpecOf(cell.CropId)))
                    count++;
            }
            RefreshAll();
            return count;
        }

        // ── 표시 ─────────────────────────────────────────────

        public int PlotCount => state != null ? state.PlotCount : 0;
        public int MaxPlotCount => settings != null ? settings.MaxPlotCount : 0;

        /// <summary>밭 구획을 늘린다 (상점 밭 구획 확장 3-3). 새 칸은 미개간. 최대를 넘으면 false.</summary>
        public bool TryAddPlots(int plots)
        {
            if (state == null || plots <= 0 || state.PlotCount + plots > settings.MaxPlotCount)
                return false;
            state.SetPlotCount(state.PlotCount + plots);
            SpawnMissingViews();
            RefreshAll();
            return true;
        }

        private void SpawnMissingViews()
        {
            if (views.Count < state.Cells.Count && TryGetComponent<FarmGrassClearer>(out var grass))
                grass.RefreshNextFrame(); // 새 칸 자리의 배경 풀을 지운다 (새 게임 · 불러오기 · 구획 확장)
            for (int i = views.Count; i < state.Cells.Count; i++)
            {
                var view = Instantiate(cellPrefab, CellPosition(i), CellRotation(), transform);
                view.Bind(this, i);
                views.Add(view);
                // 밭의 마지막 칸이 생기면 그 밭 테두리 (밭 = 이어진 4칸 — FarmLayout)
                if (bedPrefab != null && i % FarmLayout.CellsPerBed == FarmLayout.CellsPerBed - 1)
                {
                    Vector3 center = Vector3.zero;
                    for (int k = 0; k < FarmLayout.CellsPerBed; k++)
                        center += CellPosition(i - k);
                    var bed = Instantiate(bedPrefab, center / FarmLayout.CellsPerBed, CellRotation(), transform);
                    bed.name = $"Bed{i / FarmLayout.CellsPerBed:00}";
                }
            }
        }

        private void RefreshAll()
        {
            for (int i = 0; i < views.Count; i++)
                RefreshCell(i, false);
            UpdateSummary();
        }

        private void RefreshCell(int index, bool updateSummary = true, bool tillHit = false)
        {
            var cell = state.Cells[index];
            var status = state.StatusOf(index);
            float till = status == FarmCellStatus.Untilled ? 0f
                : status == FarmCellStatus.Tilling ? cell.TillProgress / settings.TillWorkRequired
                : 1f;
            views[index].ShowTill(till, tillHit);

            Color soil = status switch
            {
                FarmCellStatus.Untilled => untilledColor,
                FarmCellStatus.Tilling => Color.Lerp(untilledColor, tilledColor, cell.TillProgress / settings.TillWorkRequired),
                _ => cell.IsWet ? wetColor : tilledColor,
            };

            // 비료를 준 작물은 흙이 비료 색으로 물든다 (작물당 1회라 다시 줄 수 없음을 보여 줌)
            if (cell.IsFertilized)
                soil = Color.Lerp(soil, fertilizedColor, fertilizedTint);

            if (!cell.HasCrop)
            {
                views[index].Show(soil, false, default, 0f, null);
            }
            else
            {
                var spec = SpecOf(cell.CropId);
                var crop = CropOf(cell.CropId);
                float growth = Mathf.Clamp01((float)cell.Age / FarmRules.RequiredDays(cell, spec));
                bool ready = status == FarmCellStatus.Ready;
                Color color = crop.GreyboxColor;
                if (ready)
                    color = Color.Lerp(color, Color.white, readyBrighten);
                // 단계 프리팹(5-2): 칸이 비어 있는 단계는 null → 그레이박스
                var stages = crop.StagePrefabs;
                // 재수확 작물을 거둔 뒤에는 나무를 유지한다 (열매만 모델 애니메이션으로 — Harvest · Regrow)
                bool regrowing = cell.HarvestCount > 0 && spec.RegrowDays > 0;
                int stage = CropStageRules.StageIndex(stages.Count, growth, ready, regrowing);
                var stagePrefab = stage >= 0 ? stages[stage] : null;
                views[index].Show(soil, true, color, Mathf.Lerp(seedlingScale, grownScale, growth), stagePrefab);
                views[index].SetFruit(ready, regrowing);
            }

            if (updateSummary)
                UpdateSummary();
        }

        private void UpdateSummary()
        {
            int untilled = 0, tilling = 0, tilled = 0, growing = 0, ready = 0;
            for (int i = 0; i < state.Cells.Count; i++)
            {
                switch (state.StatusOf(i))
                {
                    case FarmCellStatus.Untilled: untilled++; break;
                    case FarmCellStatus.Tilling: tilling++; break;
                    case FarmCellStatus.Tilled: tilled++; break;
                    case FarmCellStatus.Growing: growing++; break;
                    case FarmCellStatus.Ready: ready++; break;
                }
            }
            summary = $"{state.Cells.Count}칸 · 미개간 {untilled} · 개간 중 {tilling} · 개간됨 {tilled} · 자라는 중 {growing} · 다 자람 {ready}";
        }

        // ── 작물 규칙 ─────────────────────────────────────────

        // 칸에는 작물 id만 있다 (저장 6-1도 id). 심을 때 데이터베이스에 있는지 확인하므로 여기서는 반드시 찾힌다.
        private CropData CropOf(string cropId) => database.Get<CropData>(cropId);

        // 성장일 능력치(스킬 4-3 — 예: 비옥한 땅 −1)를 건 작물 규칙. 최소 1일은 CropSpec이 지킨다.
        private CropSpec SpecOf(string cropId)
        {
            var s = CropOf(cropId).ToSpec();
            int grow = StatSystem.Apply(growDaysStat, s.GrowDays);
            return grow == s.GrowDays ? s : new CropSpec(grow, s.WetDays, s.HarvestYield, s.RegrowDays, s.MaxHarvests, s.NeedsWater);
        }

        // ── 배치 ─────────────────────────────────────────────

        private Vector3 AlongDir => settings.PlotExpandDirection.sqrMagnitude > 0f ? settings.PlotExpandDirection.normalized : Vector3.left;
        private Vector3 AcrossDir => Vector3.Cross(Vector3.up, AlongDir).normalized;
        private Quaternion CellRotation() => Quaternion.LookRotation(AcrossDir, Vector3.up);

        /// <summary>칸 중심의 월드 위치 (바닥 높이 = 이 오브젝트 높이).</summary>
        public Vector3 CellPosition(int index)
        {
            var (along, across) = FarmLayout.CellOffset(index, settings.CellsPerPlot, settings.CellPitch, settings.BedPitch, settings.PlotPitch);
            return transform.position + AlongDir * (float)along + AcrossDir * (float)across;
        }

        private bool OverlapsForbidden(Vector3 center)
        {
            float half = cellFootprint * 0.5f;
            foreach (var zone in forbiddenZones)
                if (zone != null && zone.Overlaps(center, half, half))
                    return true;
            return false;
        }

        // 시작 구획의 칸 자리를 검사해, 레벨 배치가 바뀌어 길 · 문과 겹치면 바로 알린다.
        // 확장 배열(밭 확장권)은 P1이라 시작 밭만 본다 (P-5, 2026-10-21) — 확장 자리는 기즈모 빨강 점선으로만.
        private void WarnForbiddenOverlaps()
        {
            int total = settings.StartPlotCount * settings.CellsPerPlot;
            int overlaps = 0;
            for (int i = 0; i < total; i++)
                if (OverlapsForbidden(CellPosition(i)))
                    overlaps++;
            if (overlaps > 0)
                Debug.LogWarning($"[FarmGrid] 시작 {settings.StartPlotCount}구획 중 {overlaps}칸이 금지 구역(길 · 문)과 겹침 — 씬 기즈모(빨강) 확인 후 Glow Farm/Setup/Build Farm Layout 다시 실행", this);
        }

        private void OnDrawGizmos()
        {
            if (settings == null)
                return;
            int open = state != null ? state.PlotCount : settings.StartPlotCount;
            Vector3 size = new Vector3(cellFootprint, 0f, cellFootprint);

            if (drawCells)
            {
                for (int i = 0; i < open * settings.CellsPerPlot; i++)
                {
                    Vector3 p = CellPosition(i) + Vector3.up * 0.15f;
                    Color c = OverlapsForbidden(p) ? GizmoKit.Forbidden
                        : state == null ? GizmoKit.ToolRange
                        : state.StatusOf(i) switch
                        {
                            FarmCellStatus.Ready => GizmoKit.HarvestDisplay,
                            FarmCellStatus.Growing => GizmoKit.Spawn,
                            FarmCellStatus.Tilled => GizmoKit.ToolRange,
                            FarmCellStatus.Tilling => GizmoKit.ToolRange,
                            _ => Color.gray,
                        };
                    GizmoKit.FlatBox(p, size, c, 0.2f);
                }
            }

            if (drawNextPlot && open < settings.MaxPlotCount)
            {
                // 다음 구획 자리: 칸 바깥 테두리를 빨강 점선으로, 그 안에서 금지 구역과 겹치는 칸은 빨강 칠
                var b = FarmLayout.PlotCenterBounds(open, settings.CellsPerPlot, settings.CellPitch, settings.BedPitch, settings.PlotPitch);
                float h = cellFootprint * 0.5f;
                Vector3 o = transform.position + Vector3.up * 0.15f;
                Vector3 Corner(double along, double across) => o + AlongDir * (float)along + AcrossDir * (float)across;
                GizmoKit.DashedQuad(Corner(b.AlongMin - h, b.AcrossMin - h), Corner(b.AlongMax + h, b.AcrossMin - h),
                    Corner(b.AlongMax + h, b.AcrossMax + h), Corner(b.AlongMin - h, b.AcrossMax + h), 0.4f, GizmoKit.Forbidden);
                for (int i = open * settings.CellsPerPlot; i < (open + 1) * settings.CellsPerPlot; i++)
                {
                    Vector3 p = CellPosition(i) + Vector3.up * 0.15f;
                    if (OverlapsForbidden(p))
                        GizmoKit.FlatBox(p, size, GizmoKit.Forbidden, 0.35f);
                }
            }
        }
    }
}
