using NevrianGlowFarm.Logic.Sales;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 장사 · 손님 공통. 손님 수는 입소문 모델(Logic CustomerModel, 손님수 설정안 · P-2) — 값은 기획 game_settings.csv에서 가져온다 (Import Plan CSV).
    /// 손님 종류별 값은 CustomerData(3-2)에 둔다.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Settings/Sales", fileName = "SalesSettings")]
    public class SalesSettingsData : ScriptableObject
    {
        [Header("손님 수 — 입소문 모델 (game_settings.csv)")]
        [Tooltip("1일차 손님 수 — 고정 (명, day1_customers)")]
        [SerializeField, Min(0)] private int day1Customers = 1;
        [Tooltip("1일차 손님마다 사는 개수 — 고정, 0이면 무작위 (개, day1_first_buy — 수정사항 10/28: 피피 · 미루가 3개씩)")]
        [SerializeField, Min(0)] private int day1FirstBuy = 2;
        [Tooltip("1일차 장사 뒤 단골 수 N (명, customer_regulars_after_day1)")]
        [SerializeField, Min(0)] private int regularsAfterDay1 = 2;
        [Tooltip("시장 크기 M — 가게를 알 수 있는 마을 사람 (명, customer_market_size)")]
        [SerializeField, Min(1)] private int marketSize = 40;
        [Tooltip("광고 계수 p 기본값 — 간판 스킬이 더한다 (customer_ad_rate)")]
        [SerializeField, Range(0f, 1f)] private float adRate = 0.02f;
        [Tooltip("입소문 계수 q 바탕 — × 어제 만족도 (customer_wom_rate)")]
        [SerializeField, Range(0f, 2f)] private float wordOfMouth = 0.35f;
        [Tooltip("이탈 계수 c — × (1 − 만족도) × 단골 (customer_churn)")]
        [SerializeField, Range(0f, 1f)] private float churn = 0.1f;
        [Tooltip("단골이 하루에 오는 확률 v (customer_visit_prob)")]
        [SerializeField, Range(0f, 1f)] private float visitChance = 0.5f;
        [Tooltip("장날 주기 — 0이면 장날 없음 (일, market_day_interval)")]
        [SerializeField, Min(0)] private int marketDayInterval = 7;
        [Tooltip("장날 손님 배율 (배, market_day_mult)")]
        [SerializeField, Min(0f)] private float marketDayMultiplier = 1.5f;
        [Tooltip("손님 상한 = 가구 자리 × 이 값 (명, customer_cap_per_spot)")]
        [SerializeField, Min(0)] private int capPerSpot = 3;
        [Tooltip("가구 자리 — 진열 가구(P-4) 전까지 이 값 (자리, store_spots_fruit + store_spots_shelf)")]
        [SerializeField, Min(0)] private int furnitureSpots = 8;
        [Tooltip("당일 청결 반응 최소 — 오늘 손님 × (최소 + (1 − 최소) × 청결도) (same_day_clean_min)")]
        [SerializeField, Range(0f, 1f)] private float sameDayCleanMin = 0.8f;
        [Tooltip("만족도 가중치: 청결도 (satisfaction_w_clean)")]
        [SerializeField, Range(0f, 1f)] private float satisfactionClean = 0.35f;
        [Tooltip("만족도 가중치: 정돈도 (satisfaction_w_tidy)")]
        [SerializeField, Range(0f, 1f)] private float satisfactionTidy = 0.35f;
        [Tooltip("만족도 가중치: 구매 성공률 (satisfaction_w_success)")]
        [SerializeField, Range(0f, 1f)] private float satisfactionSuccess = 0.3f;
        [Tooltip("오늘 손님 ± 무작위 폭 (0.1 = ±10%, customer_random)")]
        [SerializeField, Range(0f, 1f)] private float customerRandom = 0.1f;

        [Header("튜토리얼 고정값 — 튜토리얼이 진행 중인 날만 (기획 튜토리얼 2절, game_settings tutorial_*)")]
        [Tooltip("2일차부터 손님 수 고정 — 0이면 모델대로 (명, tutorial_day2_customers)")]
        [SerializeField, Min(0)] private int tutorialCustomers = 3;
        [Tooltip("손님마다 진열된 것을 모두 사려 한다 — 2일차 월광 4개 모두 → 레벨업 보장 (tutorial_day1_buy_all · tutorial_day2_buy_all)")]
        [SerializeField] private bool tutorialBuyAll = true;
        [Tooltip("손님 수 ± 무작위를 끈다 (tutorial_random_off)")]
        [SerializeField] private bool tutorialRandomOff = true;

        [Header("시간")]
        [Tooltip("손님이 들어오는 간격 — 0보다 크면 장사 시간 제한 없이 이 간격으로 들어오고 마지막 손님이 나가면 끝 (초, customer_spawn_interval — 수정사항 10/28)")]
        [SerializeField, Min(0f)] private float spawnInterval = 3f;
        [Tooltip("장사 시간 — 손님 간격이 0일 때만 씀 (초)")]
        [SerializeField, Min(1f)] private float salesDuration = 1f;
        [Tooltip("장사 시간 중 손님이 생성되는 앞부분 비율 — 손님 간격이 0일 때만 씀 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float spawnWindow;
        [Tooltip("동시에 가게 안에 있을 수 있는 손님 (명)")]
        [SerializeField, Min(1)] private int maxConcurrentCustomers = 1;

        [Header("손님 동작")]
        [Tooltip("기본 걷기 속도 — CustomerData 배율을 곱한다 (m/s)")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 0.1f;
        [Tooltip("진열대 앞 둘러보기 시간 (초)")]
        [SerializeField, Min(0f)] private float browseDuration;
        [Tooltip("계산 시간 (초)")]
        [SerializeField, Min(0f)] private float checkoutDuration;
        [Tooltip("손님 한 명이 사는 최소 개수 (개)")]
        [SerializeField, Min(1)] private int purchaseMin = 1;
        [Tooltip("손님 한 명이 사는 최대 개수 (개)")]
        [SerializeField, Min(1)] private int purchaseMax = 1;
        [Tooltip("단골의 장바구니로 늘어날 수 있는 최대 개수 (개, buy_extra_max)")]
        [SerializeField, Min(1)] private int purchaseExtraMax = 4;
        [Tooltip("흐트러진 진열 칸의 구매 확률 배율 (배, disorder_buy_mult — 가게 정리 P-3)")]
        [SerializeField, Range(0f, 1f)] private float disorderBuyMultiplier = 0.5f;
        [Tooltip("목표 지점 정지 거리 (m)")]
        [SerializeField, Min(0f)] private float stopDistance;
        [Tooltip("계산대 줄 간격 (m)")]
        [SerializeField, Min(0.1f)] private float queueSpacing = 0.1f;
        [Tooltip("걷기 클립 기준 속도 — 재생 1배 · 휴먼 스케일 1일 때 발이 나아가는 속도. 발이 미끄러지지 않게 걷기 재생 속도를 맞춘다 (m/s)")]
        [SerializeField, Min(0.1f)] private float walkClipSpeed = 1.62f;

        public float AdRate => adRate;
        public int FurnitureSpots => furnitureSpots;
        public int PurchaseExtraMax => Mathf.Max(PurchaseMax, purchaseExtraMax);
        public int Day1FirstBuy => day1FirstBuy;
        public int TutorialCustomers => tutorialCustomers;
        public bool TutorialBuyAll => tutorialBuyAll;
        public bool TutorialRandomOff => tutorialRandomOff;
        public float SalesDuration => salesDuration;
        public float SpawnWindow => spawnWindow;
        public int MaxConcurrentCustomers => maxConcurrentCustomers;
        public float WalkSpeed => walkSpeed;
        public float BrowseDuration => browseDuration;
        public float CheckoutDuration => checkoutDuration;
        public int PurchaseMin => purchaseMin;
        public int PurchaseMax => Mathf.Max(purchaseMin, purchaseMax);
        public float StopDistance => stopDistance;
        public float QueueSpacing => queueSpacing;
        public float WalkClipSpeed => walkClipSpeed;

        /// <summary>Logic 장사 규칙 값 (SalesRules · SalesSession).</summary>
        public SalesSpec ToSpec() => new SalesSpec(salesDuration, spawnWindow, maxConcurrentCustomers, purchaseMin, purchaseMax, disorderBuyMultiplier, spawnInterval);

        /// <summary>Logic 입소문 손님 모델 값 (CustomerModel).</summary>
        public CustomerModelSpec ToCustomerSpec() => new CustomerModelSpec(day1Customers, day1FirstBuy, regularsAfterDay1, marketSize, wordOfMouth,
            churn, visitChance, marketDayInterval, marketDayMultiplier, capPerSpot, sameDayCleanMin,
            satisfactionClean, satisfactionTidy, satisfactionSuccess, customerRandom);
    }
}
