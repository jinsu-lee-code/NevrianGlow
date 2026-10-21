using NevrianGlowFarm.Logic.Sales;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 장사 · 손님 공통 (BALANCE 2-6). 손님 수 = 반올림(min(상한, 기본 + 일차 ÷ 나눔수) × (1 + 보너스 합) × (청결 0% 배율 + (1 − 청결 0% 배율) × 청결도)).
    /// 손님 종류별 값은 CustomerData(3-2)에 둔다.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Settings/Sales", fileName = "SalesSettings")]
    public class SalesSettingsData : ScriptableObject
    {
        [Header("손님 수")]
        [Tooltip("기본 손님 수 (명)")]
        [SerializeField, Min(0)] private int baseCustomers;
        [Tooltip("일차를 나누는 수 — 일차 ÷ 이 값(내림)만큼 손님 추가 (일)")]
        [SerializeField, Min(1)] private int customerDayDivisor = 1;
        [Tooltip("보너스 · 청결도 적용 전 손님 수 상한 (명)")]
        [SerializeField, Min(1)] private int customerCap = 1;
        [Tooltip("청결도 0%일 때 손님 수 배율 — 100%면 1배 (배)")]
        [SerializeField, Range(0f, 1f)] private float dirtyCustomerMultiplier;

        [Header("시간")]
        [Tooltip("장사 시간 (초)")]
        [SerializeField, Min(1f)] private float salesDuration = 1f;
        [Tooltip("장사 시간 중 손님이 생성되는 앞부분 비율 (0~1)")]
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
        [Tooltip("목표 지점 정지 거리 (m)")]
        [SerializeField, Min(0f)] private float stopDistance;
        [Tooltip("계산대 줄 간격 (m)")]
        [SerializeField, Min(0.1f)] private float queueSpacing = 0.1f;
        [Tooltip("걷기 클립 기준 속도 — 재생 1배 · 휴먼 스케일 1일 때 발이 나아가는 속도. 발이 미끄러지지 않게 걷기 재생 속도를 맞춘다 (m/s)")]
        [SerializeField, Min(0.1f)] private float walkClipSpeed = 1.62f;

        public int BaseCustomers => baseCustomers;
        public int CustomerDayDivisor => customerDayDivisor;
        public int CustomerCap => customerCap;
        public float DirtyCustomerMultiplier => dirtyCustomerMultiplier;
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
        public SalesSpec ToSpec() => new SalesSpec(baseCustomers, customerDayDivisor, customerCap, dirtyCustomerMultiplier,
            salesDuration, spawnWindow, maxConcurrentCustomers, purchaseMin, purchaseMax);
    }
}
