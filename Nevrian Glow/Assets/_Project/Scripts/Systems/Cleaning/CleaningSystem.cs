using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic;
using NevrianGlowFarm.Logic.Cleaning;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Systems.Farm;
using NevrianGlowFarm.Systems.Items;
using NevrianGlowFarm.Systems.Shop;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Cleaning
{
    /// <summary>
    /// 가게 청소 · 정리 (노션 GDD 12.4, 청소 v2 설정안 · TASKS P-3). Shop 씬에 하나. 이 오브젝트 위치 = 가게 바닥 원점, 생성 구역은 로컬 x · z 사각형.
    /// 청소·진열 단계에 들어설 때(그날 처음 한 번) 어제 장사의 흔적을 놓는다 — 쓰레기(1일차 30 · 평소 1~3) · 얼룩(1일차 12 · 어제 손님 × 0.3) ·
    /// 흩어진 상품(진열대에서 빼서 바닥에, 진짜 재고) · 흐트러진 진열 칸. 놓을 자리는 포아송 배치(금지 구역 · 바닥 아이템 · 서로 사이 간격).
    /// 쓰레기 · 바닥 상품은 날이 지나도 남고(바닥 아이템), 얼룩 · 흐트러짐은 날마다 새로. 청결도 · 정돈도는 장사 단계가 시작될 때 확정한다.
    /// </summary>
    public class CleaningSystem : MonoBehaviour, Tutorial.ITutorialHighlightSource
    {
        private const int ScatterPurpose = 3; // 쓰레기 · 얼룩 · 흩어진 상품 배치용 난수 흐름
        private const int MessPurpose = 7;    // 흩어질 상품 · 흐트러질 칸 고르기

        [Header("참조")]
        [Tooltip("개수 규칙 · 간격 · 쓰레기 아이템 · 등급 (Data/Stats/CleaningSettings)")]
        [SerializeField] private CleaningSettingsData settings;
        [Tooltip("어지러움 생성 배율 능력치 — 청결 유지 스킬 (Data/Stats/stat_mess_rate, 비우면 1)")]
        [SerializeField] private StatData messRateStat;
        [Tooltip("바닥 아이템 프리팹 (Prefabs/Items/world_item) — 쓰레기로 쓴다")]
        [SerializeField] private WorldItem worldItemPrefab;
        [Tooltip("얼룩 프리팹 (Prefabs/Cleaning/stain)")]
        [SerializeField] private Stain stainPrefab;
        [Tooltip("놓으면 안 되는 곳 (계산대 · 진열대 앞 · 입구 · 쓰레기통 · 창고)")]
        [SerializeField] private List<ForbiddenZone> forbiddenZones = new List<ForbiddenZone>();

        [Header("조정값")]
        [Tooltip("생성 구역 로컬 최소 x · z (m, BALANCE 9-1: −5, −4)")]
        [SerializeField] private Vector2 areaMin = new Vector2(-5f, -4f);
        [Tooltip("생성 구역 로컬 최대 x · z (m, BALANCE 9-1: 5, 3)")]
        [SerializeField] private Vector2 areaMax = new Vector2(5f, 3f);
        [Tooltip("배치 난수 시드 — 같은 시드 · 같은 일차면 같은 배치")]
        [SerializeField] private int seed = 20261010;
        [Tooltip("금지 구역 검사에 쓰는 쓰레기 · 얼룩 크기 반 (m)")]
        [SerializeField, Min(0.05f)] private float itemHalfSize = 0.25f;

        [Header("기즈모")]
        [Tooltip("생성 구역(초록) · 미리보기 점")]
        [SerializeField] private bool drawArea = true;

        [Header("디버그")]
        [Tooltip("오늘 배치한 일차 (읽기 전용)")]
        [SerializeField] private int spawnedDay;
        [Tooltip("오늘 처음 쓰레기 수 = 오늘 생성 + 전날 남은 바닥 쓰레기 (읽기 전용)")]
        [SerializeField] private int initialTrash;
        [Tooltip("오늘 처음 얼룩 수 (읽기 전용)")]
        [SerializeField] private int initialStains;
        [Tooltip("장사 시작 때 확정한 청결도 (0~1, −1 = 아직, 읽기 전용)")]
        [SerializeField] private float lockedCleanliness = -1f;
        [Tooltip("오늘 처음 바닥 상품 수 = 오늘 흩어진 상품 + 전날 남은 바닥 상품 (읽기 전용)")]
        [SerializeField] private int initialScattered;
        [Tooltip("오늘 처음 흐트러진 진열 칸 수 (읽기 전용)")]
        [SerializeField] private int initialDisordered;
        [Tooltip("장사 시작 때 확정한 정돈도 (0~1, −1 = 아직, 읽기 전용)")]
        [SerializeField] private float lockedTidiness = -1f;

        private static CleaningSystem instance;
        private readonly List<Stain> stains = new List<Stain>();
        private readonly List<ShelfDisplay> shelves = new List<ShelfDisplay>();
        private readonly List<Vector3> previewTrash = new List<Vector3>();
        private readonly List<Vector3> previewStains = new List<Vector3>();

        public static CleaningSystem Instance => instance;
        public CleaningSettingsData Settings => settings;
        public int InitialTrash => initialTrash;
        public int InitialStains => initialStains;
        /// <summary>장사 시작 때 확정한 청결도. 아직이면 −1 — 손님 수(3-1)가 읽는다.</summary>
        public float LockedCleanliness => lockedCleanliness;
        /// <summary>장사 시작 때 확정한 정돈도. 아직이면 −1 — 손님 만족도 · 정산 등급이 읽는다.</summary>
        public float LockedTidiness => lockedTidiness;
        public int InitialScattered => initialScattered;
        public int InitialDisordered => initialDisordered;
        public IReadOnlyList<Stain> Stains => stains;

        /// <summary>청결 · 정돈 등급 (S ≥ 0.95 · A ≥ 0.80 · B ≥ 0.60 · C).</summary>
        public TidyGrade GradeOf(float value) => TidyRules.Grade(value, settings.ToMessSpec());

        /// <summary>남은 얼룩 수 (가게 HUD '오늘의 정리').</summary>
        public int StainsLeft
        {
            get
            {
                int n = 0;
                foreach (var s in stains)
                    if (s != null && !s.IsClean)
                        n++;
                return n;
            }
        }

        private void Awake()
        {
            if (settings == null || settings.TrashItem == null || worldItemPrefab == null || stainPrefab == null)
            {
                Debug.LogError("[CleaningSystem] 참조가 비어 있음 — 인스펙터 · CleaningSettings의 쓰레기 아이템 확인", this);
                enabled = false;
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void OnEnable()
        {
            GameEvents.PhaseChanged += OnPhaseChanged;
            Tutorial.TutorialHighlights.Register(this);
        }

        private void OnDisable()
        {
            GameEvents.PhaseChanged -= OnPhaseChanged;
            Tutorial.TutorialHighlights.Unregister(this);
        }

        // 튜토리얼 강조 (5-7): stains = 플레이어에게 가장 가까운 남은 얼룩, trash_nearest = 가장 가까운 바닥 쓰레기
        public bool TryGetHighlight(string name, string arg, out Tutorial.HighlightHit hit)
        {
            hit = default;
            if (name != "stains" && name != "trash_nearest")
                return false;
            var cam = Camera.main;
            Vector3 from = cam != null ? cam.transform.position : transform.position;
            Transform best = null;
            float bestDist = float.MaxValue;
            if (name == "stains")
            {
                foreach (var s in stains)
                    if (s != null && !s.IsClean && Nearer(s.transform, from, ref bestDist))
                        best = s.transform;
            }
            else
            {
                foreach (var item in WorldItem.All)
                    if (item != null && item.Item == settings.TrashItem && item.gameObject.scene == gameObject.scene && Nearer(item.transform, from, ref bestDist))
                        best = item.transform;
            }
            if (best == null)
                return false;
            hit = Tutorial.HighlightHit.At(best.position + Vector3.up * 0.6f);
            return true;
        }

        private static bool Nearer(Transform t, Vector3 from, ref float bestDist)
        {
            float d = (t.position - from).sqrMagnitude;
            if (d >= bestDist)
                return false;
            bestDist = d;
            return true;
        }

        private void OnPhaseChanged(PhaseChange change)
        {
            if (change.Phase == DayPhase.CleanDisplay && spawnedDay != change.Day)
                SpawnForDay(change.Day);
            else if (change.Phase == DayPhase.Sales && change.PreviousPhase != DayPhase.Sales)
            {
                lockedCleanliness = CurrentCleanliness();
                lockedTidiness = CurrentTidiness();
            }
        }

        // ── 하루 배치 ─────────────────────────────────────────

        /// <summary>그날 생길 양 (어제 손님 · 지금 진열 기준, 청결 유지 스킬 반영).</summary>
        public readonly struct DayMess
        {
            public readonly int Trash, Stains, Scattered, Disordered;
            public DayMess(int trash, int stains, int scattered, int disordered)
            {
                Trash = trash; Stains = stains; Scattered = scattered; Disordered = disordered;
            }
        }

        /// <summary>
        /// 그날의 어지러움을 놓는다: 흩어진 상품(진열대에서 빼서 바닥에) → 흐트러진 칸 → 쓰레기 · 얼룩.
        /// 남은 얼룩은 치우고, 바닥 쓰레기 · 바닥 상품은 그대로 두어 처음 개수에 더한다.
        /// </summary>
        public void SpawnForDay(int day)
        {
            ClearStains();
            CollectShelves();
            foreach (var s in shelves)
                s.ClearDisorder();
            int carryTrash = FloorTrashCount();
            int carryProducts = FloorProductCount();

            var mess = CountFor(day);
            var pickRandom = new SeededRandom(SeededRandom.SeedFor(seed, MessPurpose, day));
            var states = ShelfStates();

            // 흩어진 상품: 진열된 것 중 재고 비례로 골라 진열대에서 뺀다 (진짜 재고 — 다시 진열해야 팔림)
            var scattered = new List<CropData>();
            for (int n = 0; n < mess.Scattered; n++)
            {
                if (!Logic.Sales.SalesRules.TryPickUnit(states, pickRandom, out int shelf, out int slot))
                    break;
                var crop = shelves[shelf].TryTakeForSale(slot);
                if (crop != null)
                    scattered.Add(crop);
            }
            // 흐트러진 진열: 상품이 남은 칸 중에서
            int disordered = 0;
            foreach (var (shelf, slot) in TidyRules.PickDisorderSlots(states, mess.Disordered, pickRandom))
                if (shelves[shelf].SetDisordered(slot))
                    disordered++;

            var (trashPoints, stainPoints, productPoints) = Plan(day, mess.Trash, mess.Stains, scattered.Count);
            for (int i = 0; i < trashPoints.Count; i++)
            {
                // 방향은 순서로 정한다 (황금각) — 배치와 마찬가지로 같은 날이면 같은 모습.
                var trash = Instantiate(worldItemPrefab, trashPoints[i], Quaternion.Euler(0f, (i * 137.5f) % 360f, 0f), transform);
                trash.Init(settings.TrashItem, 1);
            }
            foreach (var p in stainPoints)
                stains.Add(Instantiate(stainPrefab, p, Quaternion.identity, transform));
            for (int i = 0; i < scattered.Count; i++)
            {
                // 바닥에 놓을 자리를 못 찾은 상품은 진열대에 되돌린다 (재고가 사라지지 않게)
                if (i >= productPoints.Count)
                {
                    ReturnToShelf(scattered[i]);
                    continue;
                }
                var item = Instantiate(worldItemPrefab, productPoints[i], Quaternion.Euler(0f, (i * 97f) % 360f, 0f), transform);
                item.Init(scattered[i], 1);
            }

            spawnedDay = day;
            initialTrash = carryTrash + trashPoints.Count;
            initialStains = stainPoints.Count;
            initialScattered = carryProducts + Mathf.Min(scattered.Count, productPoints.Count);
            initialDisordered = disordered;
            lockedCleanliness = -1f;
            lockedTidiness = -1f;
            Debug.Log($"[CleaningSystem] {day}일차 가게 정리: 쓰레기 {trashPoints.Count} · 얼룩 {stainPoints.Count} · 흩어진 상품 {initialScattered - carryProducts} · 흐트러진 칸 {disordered} (어제 손님 {CustomersYesterday(day)})");
            if (trashPoints.Count < mess.Trash)
                Debug.LogWarning($"[CleaningSystem] {day}일차 쓰레기 {mess.Trash}개 중 {trashPoints.Count}개만 놓음 — 생성 구역이 좁거나 금지 구역 · 바닥 아이템이 많음", this);
            if (stainPoints.Count < mess.Stains)
                Debug.LogWarning($"[CleaningSystem] {day}일차 얼룩 {mess.Stains}개 중 {stainPoints.Count}개만 놓음 — 생성 구역이 좁거나 쓰레기 · 바닥 아이템이 자리를 차지함", this);
        }

        /// <summary>그날 생길 양을 계산만 한다 (배치 · 미리보기 공통).</summary>
        public DayMess CountFor(int day)
        {
            var spec = settings.ToMessSpec();
            double messRate = StatSystem.Apply(messRateStat, 1.0);
            int customers = CustomersYesterday(day);
            var countRandom = new SeededRandom(SeededRandom.SeedFor(seed, ScatterPurpose + 100, day));
            var states = ShelfStates();
            int unsold = 0;
            foreach (var s in states)
                unsold += s.TotalCount;
            int scatter = TidyRules.ScatterCount(day, unsold, customers, messRate, spec);
            int filledAfter = Mathf.Max(0, TidyRules.FilledSlots(states)); // 흩어진 뒤 빈 칸이 생길 수 있어 실제 수는 배치 때 줄 수 있다
            // 튜토리얼(5-7)이 오늘 "쓰레기 n개 버리기 · 얼룩 n개 닦기"를 안내하면 그만큼은 생기게 (기획 고정값: 2일차 발자국 2 — 손님 × 0.3으로는 0개)
            int trash = Mathf.Max(TidyRules.TrashCount(day, spec, countRandom), Mathf.CeilToInt(Systems.Tutorial.TutorialSystem.RequiredToday(Logic.Tutorial.TutorialEvents.Trashed)));
            int stains = Mathf.Max(TidyRules.StainCount(day, customers, messRate, spec), Mathf.CeilToInt(Systems.Tutorial.TutorialSystem.RequiredToday(Logic.Tutorial.TutorialEvents.StainsCleared)));
            if (Systems.Tutorial.TutorialSystem.IsActive && day > 1)
                stains = Mathf.Max(stains, settings.TutorialStains); // 기획 고정값 tutorial_day2_stains
            return new DayMess(trash, stains, scatter, TidyRules.DisorderCount(day, filledAfter, messRate, spec));
        }

        /// <summary>그날 놓을 자리를 계산만 한다 (배치 · 미리보기 공통). 지금 바닥 아이템을 피한다.</summary>
        public (List<Vector3> Trash, List<Vector3> Stains, List<Vector3> Products) Plan(int day, int trashCount, int stainCount, int productCount)
        {
            var occupied = new List<FloorPoint>();
            foreach (var item in WorldItem.All)
                if (item != null && item.gameObject.scene == gameObject.scene)
                    occupied.Add(ToLocal(item.transform.position));

            var random = new SeededRandom(SeededRandom.SeedFor(seed, ScatterPurpose, day));
            var trash = CleaningRules.Scatter(trashCount, areaMin.x, areaMin.y, areaMax.x, areaMax.y, settings.MinSpacing, settings.PlacementAttempts, random, Allowed, occupied);
            occupied.AddRange(trash);
            var stain = CleaningRules.Scatter(stainCount, areaMin.x, areaMin.y, areaMax.x, areaMax.y, settings.MinSpacing, settings.PlacementAttempts, random, AllowedForStain, occupied);
            occupied.AddRange(stain);
            var products = CleaningRules.Scatter(productCount, areaMin.x, areaMin.y, areaMax.x, areaMax.y, settings.MinSpacing, settings.PlacementAttempts, random, Allowed, occupied);
            return (trash.ConvertAll(ToWorld), stain.ConvertAll(ToWorld), products.ConvertAll(ToWorld));
        }

        /// <summary>미리보기용: 그날 양으로 자리만 계산.</summary>
        public (List<Vector3> Trash, List<Vector3> Stains, List<Vector3> Products) Plan(int day)
        {
            var mess = CountFor(day);
            return Plan(day, mess.Trash, mess.Stains, mess.Scattered);
        }

        // 어제 정산의 손님 수 (1일차 · 기록이 없으면 0)
        private static int CustomersYesterday(int day)
        {
            var game = GameManager.Instance;
            return game != null && day > 1 && game.Run.TryGet(day - 1, out var report) ? report.Customers : 0;
        }

        private void CollectShelves()
        {
            // 진열대는 이 씬(가게)에 있다 — 하루 한 번 찾는다 (프레임마다 아님)
            shelves.Clear();
            var found = FindObjectsByType<ShelfDisplay>(FindObjectsSortMode.None);
            System.Array.Sort(found, (a, b) => string.CompareOrdinal(a.name, b.name));
            shelves.AddRange(found);
        }

        private List<Logic.Shop.ShelfState> ShelfStates()
        {
            if (shelves.Count == 0)
                CollectShelves();
            var list = new List<Logic.Shop.ShelfState>(shelves.Count);
            foreach (var s in shelves)
                list.Add(s.State);
            return list;
        }

        private void ReturnToShelf(CropData crop)
        {
            foreach (var s in shelves)
                if (s.TryReturn(crop))
                    return;
            Debug.LogWarning($"[CleaningSystem] 흩어질 상품 {crop.Id}를 바닥에도 진열대에도 놓지 못함", this);
        }

        // ── 정돈도 ──────────────────────────────────────────

        /// <summary>지금 정돈도 (청소 v2): 바닥에 남은 상품 + 흐트러진 채 남은 진열 칸만 감점. 들고 있는 상품은 치운 것.</summary>
        public float CurrentTidiness() =>
            (float)TidyRules.Tidiness(initialScattered, initialDisordered, FloorProductCount(), DisorderedNow());

        /// <summary>가게 바닥(이 씬)에 놓인 상품(작물) 개수.</summary>
        public int FloorProductCount()
        {
            int count = 0;
            foreach (var item in WorldItem.All)
                if (item != null && item.Item is CropData && item.gameObject.scene == gameObject.scene)
                    count += item.Count;
            return count;
        }

        /// <summary>흐트러진 채 남은 진열 칸 수.</summary>
        public int DisorderedNow()
        {
            int n = 0;
            foreach (var s in ShelfStates())
                n += s.DisorderedCount;
            return n;
        }

        // ── 청결도 ──────────────────────────────────────────

        /// <summary>지금 청결도 (GDD 4-4): 가게 바닥에 남은 쓰레기와 얼룩 양만 감점. 들고 있는 쓰레기는 치운 것.</summary>
        public float CurrentCleanliness() =>
            (float)CleaningRules.Cleanliness(initialTrash, initialStains, FloorTrashCount(), StainAmountSum());

        /// <summary>가게 바닥(이 씬)에 놓인 쓰레기 개수.</summary>
        public int FloorTrashCount()
        {
            int count = 0;
            foreach (var item in WorldItem.All)
                if (item != null && item.Item == settings.TrashItem && item.gameObject.scene == gameObject.scene)
                    count += item.Count;
            return count;
        }

        public float StainAmountSum()
        {
            float sum = 0f;
            foreach (var s in stains)
                if (s != null && !s.IsClean)
                    sum += s.Amount;
            return sum;
        }

        // ── 대걸레 ──────────────────────────────────────────

        /// <summary>닦는다: point에서 radius 안에 중심이 있는 얼룩마다 amount만큼 줄인다. 닦은 얼룩 수.</summary>
        public int Mop(Vector3 point, float radius, float amount)
        {
            int wiped = 0, cleared = 0;
            foreach (var s in stains)
            {
                if (s == null || s.IsClean)
                    continue;
                Vector3 d = s.transform.position - point;
                d.y = 0f;
                if (d.magnitude <= radius && s.Wipe(amount) > 0f)
                {
                    wiped++;
                    if (s.IsClean)
                        cleared++;
                }
            }
            if (cleared > 0)
                GameEvents.RaiseStainsCleared(cleared);
            return wiped;
        }

        /// <summary>디버그: 바닥 쓰레기와 얼룩을 모두 치운다.</summary>
        public void DebugCleanAll()
        {
            for (int i = WorldItem.All.Count - 1; i >= 0; i--)
            {
                var item = WorldItem.All[i];
                if (item != null && item.Item == settings.TrashItem && item.gameObject.scene == gameObject.scene)
                    Destroy(item.gameObject);
            }
            foreach (var s in stains)
                if (s != null && !s.IsClean)
                    s.Wipe(1f);
        }

        // ── 에디터 미리보기 ─────────────────────────────────

        /// <summary>인스펙터 "배치 미리보기": 그날 놓을 자리를 기즈모로만 보여 준다 (아무것도 만들지 않음).</summary>
        public void Preview(int day)
        {
            var (trash, stain, _) = Plan(day);
            previewTrash.Clear(); previewTrash.AddRange(trash);
            previewStains.Clear(); previewStains.AddRange(stain);
        }

        public void ClearPreview()
        {
            previewTrash.Clear();
            previewStains.Clear();
        }

        public int PreviewTrashCount => previewTrash.Count;
        public int PreviewStainCount => previewStains.Count;

        /// <summary>
        /// 불러오기 (6-1): 오늘 놓은 얼룩을 치우고 "오늘 배치 안 함"으로 되돌린다 → 청소·진열 단계에 들어가면 다시 놓는다.
        /// 바닥 쓰레기는 바닥 아이템이라 저장 내용으로 따로 돌아온다.
        /// </summary>
        public void ResetForLoad()
        {
            ClearStains();
            spawnedDay = 0;
            lockedCleanliness = -1f;
            lockedTidiness = -1f;
            initialScattered = 0;
            initialDisordered = 0;
        }

        // ── 내부 ────────────────────────────────────────────

        private void ClearStains()
        {
            foreach (var s in stains)
                if (s != null)
                    Destroy(s.gameObject);
            stains.Clear();
        }

        private bool Allowed(FloorPoint local) => Allowed(local, false);

        // 얼룩 · 발자국: 통로처럼 '얼룩은 허용'인 금지 구역에는 생겨도 된다 (기획 회신 10/22 A2 — 통로를 걸은 발자국)
        private bool AllowedForStain(FloorPoint local) => Allowed(local, true);

        private bool Allowed(FloorPoint local, bool stain)
        {
            Vector3 world = ToWorld(local);
            foreach (var zone in forbiddenZones)
                if (zone != null && !(stain && zone.AllowsStains) && zone.Overlaps(world, itemHalfSize, itemHalfSize))
                    return false;
            return true;
        }

        private FloorPoint ToLocal(Vector3 world)
        {
            Vector3 l = transform.InverseTransformPoint(world);
            return new FloorPoint(l.x, l.z);
        }

        private Vector3 ToWorld(FloorPoint local) => transform.TransformPoint(new Vector3((float)local.X, 0f, (float)local.Z));

        private void OnDrawGizmos()
        {
            if (!drawArea)
                return;
            Vector3 center = transform.TransformPoint(new Vector3((areaMin.x + areaMax.x) * 0.5f, 0.02f, (areaMin.y + areaMax.y) * 0.5f));
            GizmoKit.FlatBox(center, new Vector3(areaMax.x - areaMin.x, 0f, areaMax.y - areaMin.y), GizmoKit.Spawn, 0.06f);
            foreach (var p in previewTrash)
                GizmoKit.Point(p + Vector3.up * 0.15f, 0.15f, GizmoKit.Spawn);
            foreach (var p in previewStains)
                GizmoKit.WirePoint(p + Vector3.up * 0.02f, 0.45f, GizmoKit.Spawn);
        }
    }
}
