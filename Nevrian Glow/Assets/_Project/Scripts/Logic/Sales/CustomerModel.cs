using System;

namespace NevrianGlowFarm.Logic.Sales
{
    /// <summary>입소문 손님 모델 값 (기획 game_settings.csv customer_* · satisfaction_* · day1_* — SalesSettingsData.ToCustomerSpec).</summary>
    public readonly struct CustomerModelSpec
    {
        /// <summary>1일차 손님 수 (고정).</summary>
        public int Day1Customers { get; }
        /// <summary>1일차 첫 손님이 사는 개수 (고정, 0이면 무작위).</summary>
        public int Day1FirstBuy { get; }
        /// <summary>1일차 장사 후 단골 수 N.</summary>
        public double RegularsAfterDay1 { get; }
        /// <summary>시장 크기 M.</summary>
        public double MarketSize { get; }
        /// <summary>입소문 계수 q의 바탕 (× 어제 만족도).</summary>
        public double WordOfMouth { get; }
        /// <summary>이탈 계수 c.</summary>
        public double Churn { get; }
        /// <summary>단골이 하루에 오는 확률 v.</summary>
        public double VisitChance { get; }
        /// <summary>장날 주기 (일, 0이면 없음).</summary>
        public int MarketDayInterval { get; }
        public double MarketDayMultiplier { get; }
        /// <summary>손님 상한 = 가구 자리 × 이 값.</summary>
        public int CapPerSpot { get; }
        /// <summary>당일 청결 반응: 오늘 손님 × (min + (1 − min) × 청결도).</summary>
        public double SameDayCleanMin { get; }
        public double WeightClean { get; }
        public double WeightTidy { get; }
        public double WeightSuccess { get; }
        /// <summary>오늘 손님 ± 무작위 폭 (0.1 = ±10%).</summary>
        public double RandomRange { get; }

        public CustomerModelSpec(int day1Customers, int day1FirstBuy, double regularsAfterDay1, double marketSize, double wordOfMouth,
            double churn, double visitChance, int marketDayInterval, double marketDayMultiplier, int capPerSpot, double sameDayCleanMin,
            double weightClean, double weightTidy, double weightSuccess, double randomRange)
        {
            Day1Customers = Math.Max(0, day1Customers);
            Day1FirstBuy = Math.Max(0, day1FirstBuy);
            RegularsAfterDay1 = Math.Max(0, regularsAfterDay1);
            MarketSize = Math.Max(1, marketSize);
            WordOfMouth = Math.Max(0, wordOfMouth);
            Churn = Clamp01(churn);
            VisitChance = Clamp01(visitChance);
            MarketDayInterval = Math.Max(0, marketDayInterval);
            MarketDayMultiplier = Math.Max(0, marketDayMultiplier);
            CapPerSpot = Math.Max(0, capPerSpot);
            SameDayCleanMin = Clamp01(sameDayCleanMin);
            WeightClean = Math.Max(0, weightClean);
            WeightTidy = Math.Max(0, weightTidy);
            WeightSuccess = Math.Max(0, weightSuccess);
            RandomRange = Clamp01(randomRange);
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }

    /// <summary>
    /// 입소문 손님 모델 (손님수 설정안 · 최종 기획서 7-3, 배스 확산 + 이탈). 엔진 의존 없음 — 장사 · 측정 봇 · 테스트가 같은 코드를 쓴다.
    /// 밤: 새 단골 = (p + q × N ÷ M) × (M − N), q = 입소문 × 어제 만족도, 떠난 단골 = c × (1 − 만족도) × N, N 갱신(0 ~ M).
    /// 장사 시작: 오늘 손님 = (N × v + 새 단골) × 장날 × (min + (1 − min) × 청결도) × (1 ± 무작위), 상한 = 가구 자리 × 3, 0.5 올림.
    /// 1일차는 손님 수 고정, 1일차 장사 뒤 단골 = 정해진 수. 저장 대상(6-1): 단골 수 · 오늘 새 단골 · 어제 만족도.
    /// </summary>
    public sealed class CustomerModel
    {
        /// <summary>단골 수 N (소수 — 모델 값 그대로 이어 간다).</summary>
        public double Regulars { get; private set; }
        /// <summary>오늘(밤에 계산한) 새 단골.</summary>
        public double NewToday { get; private set; }
        /// <summary>가장 최근 장사의 만족도 S (0~1) — 다음 밤의 입소문 · 이탈.</summary>
        public double Satisfaction { get; private set; } = 1.0;
        /// <summary>모델이 맞춰진 일차 (1 = 아직 1일차).</summary>
        public int Day { get; private set; } = 1;

        /// <summary>새 게임: 1일차 상태.</summary>
        public void Reset()
        {
            Regulars = 0;
            NewToday = 0;
            Satisfaction = 1.0;
            Day = 1;
        }

        /// <summary>저장에서 되돌린다.</summary>
        public void Load(int day, double regulars, double newToday, double satisfaction)
        {
            Day = Math.Max(1, day);
            Regulars = Math.Max(0, regulars);
            NewToday = Math.Max(0, newToday);
            Satisfaction = Clamp01(satisfaction);
        }

        /// <summary>
        /// 밤 처리: newDay(2 이상)의 새 단골 · 단골 수를 정한다. adRate = 광고 계수 p (간판 스킬 반영값).
        /// 1일차 다음 밤은 단골을 '1일차 장사 후 단골 수'로 두고 시작한다. 같은 날을 두 번 처리하지 않는다.
        /// </summary>
        public void AdvanceNight(int newDay, double adRate, CustomerModelSpec spec)
        {
            if (newDay <= 1)
            {
                Reset();
                return;
            }
            if (newDay <= Day)
                return;
            if (Day <= 1)
                Regulars = Math.Min(spec.MarketSize, spec.RegularsAfterDay1);
            for (int d = Day + 1; d <= newDay; d++) // 건너뛴 날(디버그)도 하루씩
            {
                double m = spec.MarketSize, n = Regulars, s = Satisfaction;
                double fresh = Math.Max(0, (Math.Max(0, adRate) + spec.WordOfMouth * s * n / m) * (m - n));
                double left = spec.Churn * (1 - s) * n;
                Regulars = Math.Max(0, Math.Min(m, n + fresh - left));
                NewToday = fresh;
            }
            Day = newDay;
        }

        /// <summary>오늘 기대 손님 (청결 · 무작위 · 상한 전). 1일차는 고정값.</summary>
        public double Expected(int day, CustomerModelSpec spec)
        {
            if (day <= 1)
                return spec.Day1Customers;
            return (Regulars * spec.VisitChance + NewToday) * MarketDayMultiplier(day, spec);
        }

        public static double MarketDayMultiplier(int day, CustomerModelSpec spec) =>
            spec.MarketDayInterval > 0 && day > 0 && day % spec.MarketDayInterval == 0 ? spec.MarketDayMultiplier : 1.0;

        /// <summary>
        /// 오늘 손님 수. cleanliness = 장사 시작 때 확정한 청결도, spots = 가구 자리, randomUnit = 0~1 난수(0.5 = 가운데).
        /// 1일차는 고정 (청결 · 무작위 없음 — 튜토리얼 첫 손님).
        /// </summary>
        public int TodayCount(int day, double cleanliness, int spots, double randomUnit, CustomerModelSpec spec)
        {
            if (day <= 1)
                return spec.Day1Customers;
            double clean = Clamp01(cleanliness);
            double x = Expected(day, spec) * (spec.SameDayCleanMin + (1 - spec.SameDayCleanMin) * clean);
            x *= 1 + (2 * Clamp01(randomUnit) - 1) * spec.RandomRange;
            if (spots > 0 && spec.CapPerSpot > 0)
                x = Math.Min(x, spots * spec.CapPerSpot);
            return Math.Max(0, (int)Math.Floor(x + 0.5 + 1e-9));
        }

        /// <summary>만족도 = 청결 · 정돈 · 구매 성공률 가중 합 (0~1). 손님이 없었으면 구매 성공률 1.</summary>
        public static double SatisfactionOf(double cleanliness, double tidiness, int customers, int buyers, CustomerModelSpec spec)
        {
            double success = customers > 0 ? (double)Math.Min(buyers, customers) / customers : 1.0;
            double sum = spec.WeightClean * Clamp01(cleanliness) + spec.WeightTidy * Clamp01(tidiness) + spec.WeightSuccess * success;
            double weights = spec.WeightClean + spec.WeightTidy + spec.WeightSuccess;
            return weights > 0 ? Clamp01(sum / weights) : 1.0;
        }

        /// <summary>장사가 끝나면 오늘 만족도를 남긴다 (다음 밤 입소문 · 이탈).</summary>
        public void RecordSales(double cleanliness, double tidiness, int customers, int buyers, CustomerModelSpec spec) =>
            Satisfaction = SatisfactionOf(cleanliness, tidiness, customers, buyers, spec);

        private static double Clamp01(double v) => double.IsNaN(v) ? 1 : v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
