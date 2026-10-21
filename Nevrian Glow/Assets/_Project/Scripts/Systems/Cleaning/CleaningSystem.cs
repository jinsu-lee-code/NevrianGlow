using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic;
using NevrianGlowFarm.Logic.Cleaning;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Systems.Farm;
using NevrianGlowFarm.Systems.Items;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Cleaning
{
    /// <summary>
    /// 가게 청소 (노션 GDD 12.4, GDD 4-4). Shop 씬에 하나. 이 오브젝트 위치 = 가게 바닥 원점, 생성 구역은 로컬 x · z 사각형.
    /// 청소·진열 단계에 들어설 때(그날 처음 한 번) 쓰레기(바닥 아이템)와 얼룩을 포아송 배치로 놓는다 — 금지 구역 · 바닥 아이템 · 서로 사이 간격.
    /// 쓰레기는 날이 지나도 남고(바닥 아이템), 얼룩은 날마다 새로 놓는다. 청결도는 장사 단계가 시작될 때 확정한다.
    /// </summary>
    public class CleaningSystem : MonoBehaviour
    {
        private const int ScatterPurpose = 3; // 쓰레기 · 얼룩 배치용 난수 흐름

        [Header("참조")]
        [Tooltip("개수 규칙 · 간격 · 쓰레기 아이템 (Data/Stats/CleaningSettings)")]
        [SerializeField] private CleaningSettingsData settings;
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

        private static CleaningSystem instance;
        private readonly List<Stain> stains = new List<Stain>();
        private readonly List<Vector3> previewTrash = new List<Vector3>();
        private readonly List<Vector3> previewStains = new List<Vector3>();

        public static CleaningSystem Instance => instance;
        public CleaningSettingsData Settings => settings;
        public int InitialTrash => initialTrash;
        public int InitialStains => initialStains;
        /// <summary>장사 시작 때 확정한 청결도. 아직이면 −1 — 손님 수(3-1)가 읽는다.</summary>
        public float LockedCleanliness => lockedCleanliness;
        public IReadOnlyList<Stain> Stains => stains;

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

        private void OnEnable() => GameEvents.PhaseChanged += OnPhaseChanged;
        private void OnDisable() => GameEvents.PhaseChanged -= OnPhaseChanged;

        private void OnPhaseChanged(PhaseChange change)
        {
            if (change.Phase == DayPhase.CleanDisplay && spawnedDay != change.Day)
                SpawnForDay(change.Day);
            else if (change.Phase == DayPhase.Sales && change.PreviousPhase != DayPhase.Sales)
                lockedCleanliness = CurrentCleanliness();
        }

        // ── 하루 배치 ─────────────────────────────────────────

        /// <summary>그날의 쓰레기 · 얼룩을 놓는다. 남은 얼룩은 치우고, 바닥 쓰레기는 그대로 두어 처음 쓰레기 수에 더한다.</summary>
        public void SpawnForDay(int day)
        {
            ClearStains();
            int carry = FloorTrashCount();

            var (trashPoints, stainPoints) = Plan(day);
            for (int i = 0; i < trashPoints.Count; i++)
            {
                // 방향은 순서로 정한다 (황금각) — 배치와 마찬가지로 같은 날이면 같은 모습.
                var trash = Instantiate(worldItemPrefab, trashPoints[i], Quaternion.Euler(0f, (i * 137.5f) % 360f, 0f), transform);
                trash.Init(settings.TrashItem, 1);
            }
            foreach (var p in stainPoints)
                stains.Add(Instantiate(stainPrefab, p, Quaternion.identity, transform));

            spawnedDay = day;
            initialTrash = carry + trashPoints.Count;
            initialStains = stainPoints.Count;
            lockedCleanliness = -1f;
            int wanted = CleaningRules.DailyCount(settings.Trash.BaseCount, day, settings.Trash.DayDivisor, settings.Trash.DayBonusCap);
            if (trashPoints.Count < wanted)
                Debug.LogWarning($"[CleaningSystem] {day}일차 쓰레기 {wanted}개 중 {trashPoints.Count}개만 놓음 — 생성 구역이 좁거나 금지 구역 · 바닥 아이템이 많음", this);
        }

        /// <summary>그날 놓을 자리를 계산만 한다 (배치 · 미리보기 공통). 지금 바닥 아이템을 피한다.</summary>
        public (List<Vector3> Trash, List<Vector3> Stains) Plan(int day)
        {
            var occupied = new List<FloorPoint>();
            foreach (var item in WorldItem.All)
                if (item != null && item.gameObject.scene == gameObject.scene)
                    occupied.Add(ToLocal(item.transform.position));

            int trashCount = CleaningRules.DailyCount(settings.Trash.BaseCount, day, settings.Trash.DayDivisor, settings.Trash.DayBonusCap);
            int stainCount = CleaningRules.DailyCount(settings.Stain.BaseCount, day, settings.Stain.DayDivisor, settings.Stain.DayBonusCap);
            var random = new SeededRandom(SeededRandom.SeedFor(seed, ScatterPurpose, day));

            var trash = CleaningRules.Scatter(trashCount, areaMin.x, areaMin.y, areaMax.x, areaMax.y, settings.MinSpacing, settings.PlacementAttempts, random, Allowed, occupied);
            occupied.AddRange(trash);
            var stain = CleaningRules.Scatter(stainCount, areaMin.x, areaMin.y, areaMax.x, areaMax.y, settings.MinSpacing, settings.PlacementAttempts, random, Allowed, occupied);
            return (trash.ConvertAll(ToWorld), stain.ConvertAll(ToWorld));
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
            int wiped = 0;
            foreach (var s in stains)
            {
                if (s == null || s.IsClean)
                    continue;
                Vector3 d = s.transform.position - point;
                d.y = 0f;
                if (d.magnitude <= radius && s.Wipe(amount) > 0f)
                    wiped++;
            }
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
            var (trash, stain) = Plan(day);
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
        }

        // ── 내부 ────────────────────────────────────────────

        private void ClearStains()
        {
            foreach (var s in stains)
                if (s != null)
                    Destroy(s.gameObject);
            stains.Clear();
        }

        private bool Allowed(FloorPoint local)
        {
            Vector3 world = ToWorld(local);
            foreach (var zone in forbiddenZones)
                if (zone != null && zone.Overlaps(world, itemHalfSize, itemHalfSize))
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
