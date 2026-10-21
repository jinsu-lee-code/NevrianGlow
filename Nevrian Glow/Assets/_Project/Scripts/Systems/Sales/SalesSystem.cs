using System;
using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Sales;
using NevrianGlowFarm.Logic.Shop;
using NevrianGlowFarm.Systems.Cleaning;
using NevrianGlowFarm.Systems.Shop;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NevrianGlowFarm.Systems.Sales
{
    /// <summary>하루 장사 결과 (정산 화면 3-3이 보여 준다).</summary>
    public readonly struct SalesResult
    {
        public int Day { get; }
        public int Customers { get; }
        public int Buyers { get; }
        public int EmptyHanded { get; }
        public int ItemsSold { get; }
        public int Revenue { get; }
        public IReadOnlyDictionary<string, int> Sold { get; }
        public bool Skipped { get; }
        /// <summary>장사가 끝날 때 받은 경험치 (매출 × 1) · 레벨업.</summary>
        public ExpGain Exp { get; }

        public SalesResult(int day, SalesSession session, bool skipped, ExpGain exp)
        {
            Exp = exp;
            Day = day;
            Customers = session.Total;
            Buyers = session.Buyers;
            EmptyHanded = session.EmptyHanded;
            ItemsSold = session.ItemsSold;
            Revenue = session.Revenue;
            var sold = new Dictionary<string, int>();
            foreach (var pair in session.Sold)
                sold[pair.Key] = pair.Value;
            Sold = sold;
            Skipped = skipped;
        }
    }

    /// <summary>
    /// 장사 단계 진행 (노션 GDD 12.4 SalesSimulator, GDD 4-5 · 장사 B안 완전 자동). Shop 씬에 하나.
    /// 장사 단계에 들어서면 손님 수(청결도 확정값)를 정하고 생성 시각마다 입구에 손님을 만든다. 규칙은 Logic SalesRules · SalesSession.
    /// 매출은 계산 때 달러에 더한다. 모든 손님이 나가면(또는 Space 건너뛰기 · 시간 초과) 경험치를 더하고 정산 단계로 넘긴다.
    /// 건너뛰기 = 가게 안 손님은 지금 사고 내고, 아직 안 온 손님은 같은 함수로 즉시 처리.
    /// </summary>
    public class SalesSystem : MonoBehaviour
    {
        private const int PurchasePurpose = 2; // 손님 구매용 난수 흐름 (청소 배치 = 3)
        private const int CustomerPurpose = 5; // 손님 종류 · 서는 자리용 난수 흐름

        [Header("참조")]
        [Tooltip("손님 수 · 시간 · 동작 (Data/Stats/SalesSettings)")]
        [SerializeField] private SalesSettingsData settings;
        [Tooltip("손님 종류(CustomerData) · 작물 가격 (Data/ContentDatabase)")]
        [SerializeField] private ContentDatabase database;
        [Tooltip("손님이 파는 진열대들")]
        [SerializeField] private List<ShelfDisplay> shelves = new List<ShelfDisplay>();
        [Tooltip("손님 입구 · 출구 (BALANCE 9-1: 0, 0, -5)")]
        [SerializeField] private Transform entrance;
        [Tooltip("장바구니 받침대 앞 (BALANCE 9-1: 1.5, 0, -4.5)")]
        [SerializeField] private Transform basketStand;
        [Tooltip("진열대 앞 손님이 서는 줄의 가운데 — 로컬 X로 둘러보기 폭만큼 흩어 선다 (BALANCE 9-1: 0, 0, 2)")]
        [SerializeField] private Transform shelfFront;
        [Tooltip("계산대 (BALANCE 9-1: 4, 0, -1)")]
        [SerializeField] private Transform counter;
        [Tooltip("줄 첫 칸 — 앞(+Z 로컬)으로 줄 간격씩 뒤 칸 (BALANCE 9-1: 4, 0, 0.5)")]
        [SerializeField] private Transform queueStart;
        [Tooltip("장사 건너뛰기 (Player/SkipSales, Space)")]
        [SerializeField] private InputActionReference skipAction;
        [Tooltip("판매가 능력치 — 작물 판매가에 곱함 (Data/Stats/stat_sell_price, 비우면 그대로)")]
        [SerializeField] private StatData sellPriceStat;
        [Tooltip("손님 수 보너스 능력치 — 손님 수 공식의 보너스 합 (Data/Stats/stat_customer_bonus, 비우면 0)")]
        [SerializeField] private StatData customerBonusStat;

        [Header("조정값")]
        [Tooltip("진열대 앞에서 손님이 흩어 서는 폭 (m)")]
        [SerializeField, Min(0f)] private float browseSpread = 4f;
        [Tooltip("장사 시간이 지난 뒤 남은 손님을 기다리는 최대 시간 — 지나면 남은 손님을 바로 처리 (초, 임시)")]
        [SerializeField, Min(0f)] private float overtimeLimit = 20f;
        [Tooltip("손님 구매 · 종류 난수 시드 — 같은 시드 · 같은 일차면 같은 장사")]
        [SerializeField] private int seed = 20261011;

        [Header("기즈모")]
        [Tooltip("입구 캡슐 · 동선(마젠타) · 지점 정지 거리 원 · 줄 칸 번호")]
        [SerializeField] private bool drawPath = true;
        [Tooltip("그릴 줄 칸 수")]
        [SerializeField, Min(1)] private int gizmoQueueSlots = 6;

        [Header("디버그")]
        [Tooltip("장사 중인지 (읽기 전용)")]
        [SerializeField] private bool running;
        [Tooltip("진행 요약 (읽기 전용)")]
        [SerializeField] private string summary;
        [Tooltip("가게 안 손님 상태 (읽기 전용)")]
        [SerializeField] private List<string> customerView = new List<string>();

        private static SalesSystem instance;
        private SalesSession session;
        private readonly CheckoutLine line = new CheckoutLine();
        private readonly List<CustomerAI> customers = new List<CustomerAI>();
        private readonly List<CustomerData> types = new List<CustomerData>();
        private readonly List<double> typeWeights = new List<double>();
        private readonly List<int> typeDays = new List<int>();
        private readonly List<ShelfState> shelfStates = new List<ShelfState>();
        private IRandomSource purchaseRandom;
        private IRandomSource customerRandom;
        private float elapsed;
        private int day;
        private int spawnedNumber;
        private SalesResult? lastResult;

        public static SalesSystem Instance => instance;
        public bool IsRunning => running;
        public SalesSession Session => session;
        public SalesResult? LastResult => lastResult;
        public float Elapsed => elapsed;
        public IReadOnlyList<CustomerAI> Customers => customers;

        /// <summary>장사가 끝났을 때 (정산 화면 3-3).</summary>
        public event Action<SalesResult> Finished;

        private void Awake()
        {
            if (settings == null || database == null || entrance == null || basketStand == null || shelfFront == null || counter == null || queueStart == null || skipAction == null)
            {
                Debug.LogError("[SalesSystem] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
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
            if (skipAction != null)
                skipAction.action.Enable();
        }

        private void OnDisable() => GameEvents.PhaseChanged -= OnPhaseChanged;

        private void OnPhaseChanged(PhaseChange change)
        {
            if (change.Phase == DayPhase.Sales && change.PreviousPhase != DayPhase.Sales)
                Begin(change.Day);
            else if (running && change.Phase != DayPhase.Sales)
                FinishNow(advancePhase: false); // 디버그로 단계를 넘긴 경우 — 남은 손님은 바로 처리
        }

        // ── 진행 ─────────────────────────────────────────

        private void Begin(int today)
        {
            ClearCustomers();
            day = today;
            var spec = settings.ToSpec();
            // 청결도는 장사 시작 때 확정 (CleaningSystem도 같은 순간 같은 값으로 고정한다). 보너스 합 = 스킬 효과 (StatSystem 4-3).
            double clean = CleaningSystem.Instance != null ? CleaningSystem.Instance.CurrentCleanliness() : 1.0;
            double bonus = StatSystem.Apply(customerBonusStat, 0.0);
            int count = SalesRules.CustomerCount(today, bonus, clean, spec);
            session = new SalesSession(count, spec);
            purchaseRandom = new SeededRandom(SeededRandom.SeedFor(seed, PurchasePurpose, today));
            customerRandom = new SeededRandom(SeededRandom.SeedFor(seed, CustomerPurpose, today));
            CollectTypes(today);
            CollectShelves();
            elapsed = 0f;
            spawnedNumber = 0;
            lastResult = null;
            running = true;
            Debug.Log($"[SalesSystem] {today}일차 장사 시작: 손님 {count}명 (청결도 {clean:P0} · 보너스 {bonus:P0}) · 진열 {DisplayedCount()}개");
            UpdateDebugView();
        }

        private void Update()
        {
            if (!running)
                return;
            elapsed += Time.deltaTime;

            if (skipAction.action.WasPressedThisFrame() && !PlayerInputGate.IsBlocked)
            {
                FinishNow(advancePhase: true);
                return;
            }

            int due = session.SpawnDue(elapsed);
            for (int i = 0; i < due; i++)
                Spawn();

            if (session.IsFinished)
                Complete(skipped: false, advancePhase: true);
            else if (elapsed >= session.Spec.Duration + overtimeLimit)
            {
                Debug.Log($"[SalesSystem] 장사 시간 + {overtimeLimit}초가 지나 남은 손님 {session.InStore + session.Total - session.Spawned}명을 바로 처리");
                FinishNow(advancePhase: true);
            }
            else
                UpdateDebugView();
        }

        /// <summary>건너뛰기: 가게 안 손님은 지금 사고 내고, 아직 안 온 손님은 같은 함수로 즉시 처리한 뒤 끝낸다.</summary>
        public void FinishNow(bool advancePhase)
        {
            if (!running)
                return;
            foreach (var c in customers.ToArray())
            {
                if (c == null)
                    continue;
                c.SettleNow();
                session.Leave();
                Destroy(c.gameObject);
            }
            customers.Clear();
            line.Clear();
            int before = session.Revenue;
            session.SkipRemaining(shelfStates, purchaseRandom, PriceOf, TakeForSaleId);
            int revenue = session.Revenue - before;
            if (revenue > 0 && GameManager.Instance != null)
                GameManager.Instance.TryChangeMoney(revenue);
            Complete(skipped: true, advancePhase: advancePhase);
        }

        private void Complete(bool skipped, bool advancePhase)
        {
            running = false;
            ClearCustomers();
            // 경험치 = 매출 × 1 (정산 화면 3-3b는 이 결과를 보여 준다)
            var gain = GameManager.Instance != null && session.Revenue > 0 ? GameManager.Instance.AddRevenueExp(session.Revenue) : default;
            var result = new SalesResult(day, session, skipped, gain);
            lastResult = result;
            Debug.Log($"[SalesSystem] {day}일차 장사 끝{(skipped ? " (건너뛰기)" : "")}: 손님 {result.Customers}명 (구매 {result.Buyers} · 빈손 {result.EmptyHanded}) · 판매 {result.ItemsSold}개 · 매출 {result.Revenue}달러");
            UpdateDebugView();
            Finished?.Invoke(result);
            if (advancePhase && GameManager.Instance != null && GameManager.Instance.Phase == DayPhase.Sales)
                GameManager.Instance.AdvancePhase();
        }

        private void Spawn()
        {
            int typeIndex = SalesRules.PickCustomerType(typeWeights, typeDays, day, customerRandom);
            var type = typeIndex >= 0 ? types[typeIndex] : null;
            if (type == null || type.Prefab == null)
            {
                // 손님 모습이 없어도 장사 결과는 같게 — 그 손님은 바로 사고 낸다
                Debug.LogWarning(type == null
                    ? $"[SalesSystem] {day}일차에 나올 손님 종류(CustomerData)가 없음 - Data/Customers 확인"
                    : $"[SalesSystem] 손님 '{type.Id}'의 prefab이 비어 있음", this);
                Pay(FillBasket());
                session.Leave();
                return;
            }
            var go = Instantiate(type.Prefab, entrance.position, entrance.rotation, transform);
            var ai = go.GetComponent<CustomerAI>();
            if (ai == null)
            {
                Debug.LogError($"[SalesSystem] 손님 '{type.Id}' 프리팹에 CustomerAI가 없음", go);
                Destroy(go);
                session.Leave();
                return;
            }
            spawnedNumber++;
            customers.Add(ai);
            ai.Begin(this, type, spawnedNumber, settings, entrance.position);
            AudioManager.Play(SoundCues.CustomerEnter, entrance.position);
        }

        private void CollectTypes(int today)
        {
            types.Clear();
            typeWeights.Clear();
            typeDays.Clear();
            foreach (var t in database.All<CustomerData>())
            {
                types.Add(t);
                typeWeights.Add(t.SpawnWeight);
                typeDays.Add(t.UnlockDay);
            }
        }

        private void CollectShelves()
        {
            shelfStates.Clear();
            foreach (var s in shelves)
                shelfStates.Add(s != null ? s.State : new ShelfState(0, 1));
        }

        /// <summary>불러오기 (6-1): 진행 중인 장사를 계산 없이 멈추고 손님을 지운다 (불러온 상태에 매출이 더해지지 않게).</summary>
        public void CancelForLoad()
        {
            running = false;
            ClearCustomers();
            session = null;
            lastResult = null;
            UpdateDebugView();
        }

        private void ClearCustomers()
        {
            foreach (var c in customers)
                if (c != null)
                    Destroy(c.gameObject);
            customers.Clear();
            line.Clear();
        }

        // ── 손님이 부르는 것 ─────────────────────────────

        public Vector3 EntrancePosition => entrance.position;
        public Vector3 BasketStandPosition => basketStand.position;
        public Vector3 CounterPosition => counter.position;

        /// <summary>진열대 앞 둘러볼 자리 — 손님마다 폭 안에서 흩어진다.</summary>
        public Vector3 BrowsePoint(int number)
        {
            float x = ((float)customerRandom.NextDouble() - 0.5f) * browseSpread;
            return shelfFront.TransformPoint(new Vector3(x, 0f, 0f));
        }

        /// <summary>둘러볼 때 바라보는 점 (진열대 쪽 = shelfFront 로컬 +Z).</summary>
        public Vector3 ShelfLookPoint(Vector3 from) => from + shelfFront.forward;

        public Vector3 QueueSlotPosition(int slot) => queueStart.position + queueStart.forward * (settings.QueueSpacing * Mathf.Max(0, slot));

        public int JoinLine(int number) => line.Join(number);
        public int LinePosition(int number) => line.PositionOf(number);
        public bool IsFront(int number) => line.IsFront(number);
        public void LeaveLine(int number) => line.Leave(number);

        /// <summary>진열대에서 장바구니를 채운다 (재고 비례 무작위 · 1~3개 · 마지막에 놓은 것부터).</summary>
        public List<CropData> FillBasket()
        {
            var ids = SalesRules.FillBasket(shelfStates, purchaseRandom, session.Spec, TakeForSaleId);
            var crops = new List<CropData>(ids.Count);
            foreach (var id in ids)
                crops.Add(database.Get<CropData>(id));
            return crops;
        }

        /// <summary>계산 — 매출을 바로 달러에 더한다 (빈 장바구니면 빈손 퇴장으로 센다).</summary>
        public void Pay(IReadOnlyList<CropData> basket)
        {
            if (session == null)
                return;
            var ids = new List<string>(basket.Count);
            foreach (var c in basket)
                if (c != null)
                    ids.Add(c.Id);
            int paid = session.Pay(ids, PriceOf);
            if (paid > 0 && GameManager.Instance != null)
                GameManager.Instance.TryChangeMoney(paid);
            if (paid > 0)
                AudioManager.Play(SoundCues.CheckoutCoin, CounterPosition);
        }

        public void Despawn(CustomerAI customer)
        {
            line.Leave(customer.Number);
            if (customers.Remove(customer))
                session?.Leave();
            AudioManager.Play(SoundCues.CustomerExit, customer.transform.position);
            Destroy(customer.gameObject);
        }

        private string TakeForSaleId(int shelf, int slot)
        {
            var crop = shelves[shelf].TryTakeForSale(slot); // 진열대 겉모습도 같이 빠진다
            return crop != null ? crop.Id : null;
        }

        private int PriceOf(string id)
        {
            var crop = database.Get<CropData>(id);
            return crop != null ? StatSystem.Apply(sellPriceStat, crop.SellPrice) : 0; // 고품질 스킬 등 (4-3)
        }

        /// <summary>지금 진열대에 놓인 작물 수 (장사 전 안내 — 영업 팻말).</summary>
        public int DisplayedNow()
        {
            int n = 0;
            foreach (var s in shelves)
                if (s != null)
                    n += s.State.TotalCount;
            return n;
        }

        private int DisplayedCount()
        {
            int n = 0;
            foreach (var s in shelfStates)
                n += s.TotalCount;
            return n;
        }

        private void UpdateDebugView()
        {
            if (session == null)
                return;
            summary = $"{elapsed:0.0}초 · 손님 {session.Spawned}/{session.Total} (안 {session.InStore}) · 구매 {session.Buyers} · 빈손 {session.EmptyHanded} · 매출 {session.Revenue}";
            customerView.Clear();
            foreach (var c in customers)
                if (c != null)
                    customerView.Add($"{c.name}: {c.State}");
        }

        private void OnDrawGizmos()
        {
            if (!drawPath || entrance == null || basketStand == null || shelfFront == null || counter == null || queueStart == null)
                return;
            float stop = settings != null ? settings.StopDistance : 0.3f;
            float spacing = settings != null ? settings.QueueSpacing : 0.8f;

            // 입구: 초록 캡슐 + 들어오는 방향 화살표
            GizmoKit.WireCapsule(entrance.position, 1.6f, 0.3f, GizmoKit.Spawn);
            GizmoKit.Arrow(entrance.position + Vector3.up * 0.05f, entrance.position + entrance.forward * 1f + Vector3.up * 0.05f, GizmoKit.Spawn);

            // 동선: 입구 → 바구니 → 진열대 앞 → 줄 끝 … 계산대 → 입구 (마젠타)
            Vector3 lift = Vector3.up * 0.05f;
            Vector3 lastSlot = queueStart.position + queueStart.forward * spacing * (gizmoQueueSlots - 1);
            GizmoKit.Arrow(entrance.position + lift, basketStand.position + lift, GizmoKit.CustomerPath);
            GizmoKit.Arrow(basketStand.position + lift, shelfFront.position + lift, GizmoKit.CustomerPath);
            GizmoKit.Arrow(shelfFront.position + lift, lastSlot + lift, GizmoKit.CustomerPath);
            GizmoKit.Arrow(lastSlot + lift, queueStart.position + lift, GizmoKit.CustomerPath);
            GizmoKit.Arrow(queueStart.position + lift, entrance.position + lift, GizmoKit.CustomerPath);
            GizmoKit.Line(shelfFront.TransformPoint(new Vector3(-browseSpread * 0.5f, 0f, 0f)) + lift, shelfFront.TransformPoint(new Vector3(browseSpread * 0.5f, 0f, 0f)) + lift, GizmoKit.CustomerPath);

            // 경유 지점 정지 거리 원
            GizmoKit.FloorCircle(basketStand.position + lift, stop, GizmoKit.CustomerPath);
            GizmoKit.FloorCircle(shelfFront.position + lift, stop, GizmoKit.CustomerPath);
            GizmoKit.FloorCircle(entrance.position + lift, stop, GizmoKit.CustomerPath);
            GizmoKit.WireBox(counter.position + Vector3.up * 0.5f, new Vector3(1.6f, 1f, 0.6f), GizmoKit.CustomerPath);

            // 줄 칸: 번호 붙은 작은 사각형
            for (int i = 0; i < gizmoQueueSlots; i++)
            {
                Vector3 p = queueStart.position + queueStart.forward * spacing * i;
                GizmoKit.FlatBox(p + lift, new Vector3(0.4f, 0f, 0.4f), GizmoKit.CustomerPath, 0.15f);
                GizmoKit.Label(p + Vector3.up * 0.3f, (i + 1).ToString(), GizmoKit.CustomerPath);
            }
        }
    }
}
