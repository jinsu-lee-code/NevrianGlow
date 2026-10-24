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
    public class SalesSystem : MonoBehaviour, Tutorial.ITutorialHighlightSource
    {
        private const int PurchasePurpose = 2; // 손님 구매용 난수 흐름 (청소 배치 = 3)
        private const int CustomerPurpose = 5; // 손님 종류 · 서는 자리용 난수 흐름
        private const int CountPurpose = 6;    // 오늘 손님 수 ± 무작위 (입소문 모델)

        [Header("참조")]
        [Tooltip("손님 수 · 시간 · 동작 (Data/Stats/SalesSettings)")]
        [SerializeField] private SalesSettingsData settings;
        [Tooltip("손님 종류(CustomerData) · 작물 가격 (Data/ContentDatabase)")]
        [SerializeField] private ContentDatabase database;
        [Tooltip("손님이 파는 진열 가구들 (과일 진열대 — Build Shop Layout이 채운다)")]
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
        [Tooltip("광고 계수 p 능력치 — 기본값(SalesSettings)에 간판 스킬이 더함 (Data/Stats/stat_customer_ad_rate, 비우면 기본값)")]
        [SerializeField] private StatData adRateStat;
        [Tooltip("손님마다 1개 더 살 확률 능력치 — 단골의 장바구니 (Data/Stats/stat_extra_buy_chance, 비우면 0)")]
        [SerializeField] private StatData extraBuyStat;

        [Header("조정값")]
        [Tooltip("진열대 앞에서 손님이 흩어 서는 폭 (m)")]
        [SerializeField, Min(0f)] private float browseSpread = 4f;
        [Tooltip("진열 가구 가장자리에서 손님이 서는 거리 (m)")]
        [SerializeField, Min(0f)] private float browseGap = 0.6f;
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
        private readonly CustomerModel model = new CustomerModel();
        private SalesSession session;
        private double todayCleanliness = 1.0;
        private double todayTidiness = 1.0;
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
        /// <summary>입소문 손님 모델 (단골 수 · 새 단골 · 어제 만족도).</summary>
        public CustomerModel Model => model;
        public SalesSettingsData Settings => settings;

        /// <summary>광고 계수 p = 기본값 + 간판 스킬.</summary>
        public double AdRate => StatSystem.Apply(adRateStat, (double)settings.AdRate);

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
            GameEvents.DayChanged += OnDayChanged;
            if (skipAction != null)
                skipAction.action.Enable();
            Tutorial.TutorialHighlights.Register(this);
        }

        private void OnDisable()
        {
            GameEvents.PhaseChanged -= OnPhaseChanged;
            GameEvents.DayChanged -= OnDayChanged;
            PlayerInputGate.ReleaseWorld(this);
            Tutorial.TutorialHighlights.Unregister(this);
        }

        // 튜토리얼 강조 (5-7): customers = 가게 안 첫 손님, customer_<id> = 그 종류의 손님 (없으면 첫 손님)
        public bool TryGetHighlight(string name, string arg, out Tutorial.HighlightHit hit)
        {
            hit = default;
            if (!name.StartsWith("customer") || customers.Count == 0)
                return false;
            CustomerAI pick = null;
            foreach (var c in customers)
                if (c != null && (pick == null || (c.Data != null && c.Data.Id == name)))
                    pick = c;
            if (pick == null)
                return false;
            hit = Tutorial.HighlightHit.At(pick.transform.position + Vector3.up * ((pick.Data != null ? pick.Data.Height : 1.6f) + 0.9f));
            return true;
        }

        // 새 게임(1일차 알림)이면 모델을 처음으로. 불러오기는 이 다음 LoadFrom이 덮는다.
        private void OnDayChanged(int day)
        {
            if (day <= 1)
                model.Reset();
        }

        private void OnPhaseChanged(PhaseChange change)
        {
            // 밤 처리: 다음 날 새 단골 · 단골 수 (손님수 설정안 2)
            if (change.DayChanged)
            {
                model.AdvanceNight(change.Day, AdRate, settings.ToCustomerSpec());
                Debug.Log($"[SalesSystem] {change.Day}일차 손님 모델: 단골 {model.Regulars:0.0} · 새 단골 {model.NewToday:0.0} · 어제 만족도 {model.Satisfaction:P0} → 기대 {model.Expected(change.Day, settings.ToCustomerSpec()):0.0}명");
            }
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
            var customerSpec = settings.ToCustomerSpec();
            // 청결도는 장사 시작 때 확정 (CleaningSystem도 같은 순간 같은 값으로 고정한다) → 당일 청결 반응 · 오늘 만족도
            double clean = CleaningSystem.Instance != null ? CleaningSystem.Instance.CurrentCleanliness() : 1.0;
            todayCleanliness = clean;
            todayTidiness = CleaningSystem.Instance != null ? CleaningSystem.Instance.CurrentTidiness() : 1.0;
            // 튜토리얼 고정값 (5-7, 기획 튜토리얼 2절 — 진행 중인 날만): 무작위 끔(가운데 값) · 2일차부터 손님 수 고정 · 손님마다 진열된 것을 모두 구매
            bool tutorial = Systems.Tutorial.TutorialSystem.IsActive;
            var countRandom = new SeededRandom(SeededRandom.SeedFor(seed, CountPurpose, today));
            double unit = tutorial && settings.TutorialRandomOff ? 0.5 : countRandom.NextDouble();
            int count = model.TodayCount(today, clean, settings.FurnitureSpots, unit, customerSpec);
            if (tutorial && today > 1 && settings.TutorialCustomers > 0)
                count = settings.TutorialCustomers;
            double extra = StatSystem.Apply(extraBuyStat, 0.0); // 단골의 장바구니
            CollectTypes(today);
            CollectShelves();
            // 모두 구매: 설정이 켜져 있거나, 오늘 튜토리얼이 레벨업을 안내하면(2일차 월광 4개 모두 → 누적 108 ≥ 100) 손님마다 진열 수만큼 사려 한다
            bool buyAll = (tutorial && settings.TutorialBuyAll) || Systems.Tutorial.TutorialSystem.RequiredToday(Logic.Tutorial.TutorialEvents.LeveledUp) > 0f;
            int everyWanted = buyAll ? Mathf.Max(1, DisplayedCount()) : 0;
            session = new SalesSession(count, spec, today <= 1 ? settings.Day1FirstBuy : 0, extra, settings.PurchaseExtraMax, everyWanted);
            purchaseRandom = new SeededRandom(SeededRandom.SeedFor(seed, PurchasePurpose, today));
            customerRandom = new SeededRandom(SeededRandom.SeedFor(seed, CustomerPurpose, today));
            elapsed = 0f;
            spawnedNumber = 0;
            lastResult = null;
            running = true;
            PlayerInputGate.BlockWorld(this); // 장사 중 이동 · 시점만 (P-7, 결정 2026-10-21) — 화면(B · Tab · K)은 열 수 있음
            Debug.Log($"[SalesSystem] {today}일차 장사 시작: 손님 {count}명 (기대 {model.Expected(today, customerSpec):0.0} · 청결도 {clean:P0} · 단골 {model.Regulars:0.0}) · 진열 {DisplayedCount()}개");
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
            PlayerInputGate.ReleaseWorld(this);
            ClearCustomers();
            // 경험치 = 매출 × 1 (정산 화면 3-3b는 이 결과를 보여 준다)
            var gain = GameManager.Instance != null && session.Revenue > 0 ? GameManager.Instance.AddRevenueExp(session.Revenue) : default;
            var result = new SalesResult(day, session, skipped, gain);
            lastResult = result;
            // 오늘 만족도 → 다음 밤 입소문 · 이탈 (청결도 · 정돈도는 장사 시작 때 확정한 값)
            model.RecordSales(todayCleanliness, todayTidiness, session.Total, session.Buyers, settings.ToCustomerSpec());
            Debug.Log($"[SalesSystem] {day}일차 장사 끝{(skipped ? " (건너뛰기)" : "")}: 손님 {result.Customers}명 (구매 {result.Buyers} · 빈손 {result.EmptyHanded}) · 판매 {result.ItemsSold}개 · 매출 {result.Revenue}달러 · 만족도 {model.Satisfaction:P0}");
            UpdateDebugView();
            Finished?.Invoke(result);
            GameEvents.RaiseSalesFinished();
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

        // ── 저장 (6-1) ──

        public void CaptureTo(Logic.Save.SaveSnapshot into)
        {
            into.HasCustomerModel = true;
            into.CustomerRegulars = model.Regulars;
            into.CustomerNewToday = model.NewToday;
            into.CustomerSatisfaction = model.Satisfaction;
        }

        /// <summary>손님 모델을 되돌린다. 손님 모델이 없던 예전 저장이면 1일차부터 만족도 100%로 다시 계산한 값.</summary>
        public void LoadFrom(Logic.Save.SaveSnapshot from)
        {
            if (from.HasCustomerModel)
                model.Load(from.Day, from.CustomerRegulars, from.CustomerNewToday, from.CustomerSatisfaction);
            else
            {
                model.Reset();
                model.AdvanceNight(from.Day, AdRate, settings.ToCustomerSpec());
            }
        }

        /// <summary>불러오기 (6-1): 진행 중인 장사를 계산 없이 멈추고 손님을 지운다 (불러온 상태에 매출이 더해지지 않게).</summary>
        public void CancelForLoad()
        {
            running = false;
            PlayerInputGate.ReleaseWorld(this);
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

        /// <summary>
        /// 둘러볼 자리 (P-4): 진열 가구가 여럿이면 재고 비례로 한 대를 골라 그 앞(손님 쪽 = 가구 로컬 +Z, 아래 통로 쪽)에서 폭 안에 흩어 선다.
        /// 진열이 모두 비었으면 아무 진열대나. 진열대가 없으면 예전처럼 shelfFront 줄.
        /// </summary>
        public Vector3 BrowsePoint(int number)
        {
            float x = ((float)customerRandom.NextDouble() - 0.5f) * browseSpread;
            var shelf = PickBrowseShelf();
            if (shelf == null)
                return shelfFront.TransformPoint(new Vector3(x, 0f, 0f));
            var box = shelf.GetComponent<BoxCollider>();
            float front = (box != null ? box.size.z * 0.5f : 0.5f) + browseGap;
            Vector3 p = shelf.transform.TransformPoint(new Vector3(x * 0.5f, 0f, front));
            p.y = shelf.transform.position.y;
            return p;
        }

        /// <summary>둘러볼 때 바라보는 점 — 가장 가까운 진열대 (없으면 shelfFront 앞).</summary>
        public Vector3 ShelfLookPoint(Vector3 from)
        {
            ShelfDisplay nearest = null;
            float best = float.MaxValue;
            foreach (var s in shelves)
            {
                if (s == null)
                    continue;
                float d = (s.transform.position - from).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = s;
                }
            }
            return nearest != null ? nearest.transform.position : from + shelfFront.forward;
        }

        private ShelfDisplay PickBrowseShelf()
        {
            int total = 0, count = 0;
            foreach (var s in shelves)
                if (s != null)
                {
                    total += s.State.TotalCount;
                    count++;
                }
            if (count == 0)
                return null;
            if (total <= 0)
            {
                int k = customerRandom.NextInt(0, count);
                foreach (var s in shelves)
                    if (s != null && k-- == 0)
                        return s;
            }
            int pick = customerRandom.NextInt(0, total);
            foreach (var s in shelves)
            {
                if (s == null)
                    continue;
                if (pick < s.State.TotalCount)
                    return s;
                pick -= s.State.TotalCount;
            }
            return null;
        }

        public Vector3 QueueSlotPosition(int slot) => queueStart.position + queueStart.forward * (settings.QueueSpacing * Mathf.Max(0, slot));

        public int JoinLine(int number) => line.Join(number);
        public int LinePosition(int number) => line.PositionOf(number);
        public bool IsFront(int number) => line.IsFront(number);
        public void LeaveLine(int number) => line.Leave(number);

        /// <summary>진열대에서 장바구니를 채운다 (재고 비례 무작위 · 1~3개 · 마지막에 놓은 것부터).</summary>
        public List<CropData> FillBasket()
        {
            var ids = SalesRules.FillBasket(shelfStates, purchaseRandom, session.NextWanted(purchaseRandom), TakeForSaleId, session.Spec.DisorderBuyMultiplier);
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
