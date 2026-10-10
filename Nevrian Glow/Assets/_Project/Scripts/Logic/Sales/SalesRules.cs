using System;
using System.Collections.Generic;
using NevrianGlowFarm.Logic.Shop;

namespace NevrianGlowFarm.Logic.Sales
{
    /// <summary>장사 규칙 값 (BALANCE 2-6, 데이터 = Data/Stats/SalesSettings → SalesSettingsData.ToSpec).</summary>
    public readonly struct SalesSpec
    {
        public int BaseCustomers { get; }
        public int DayDivisor { get; }
        public int CustomerCap { get; }
        public double DirtyMultiplier { get; }
        public double Duration { get; }
        public double SpawnWindow { get; }
        public int MaxConcurrent { get; }
        public int PurchaseMin { get; }
        public int PurchaseMax { get; }

        public SalesSpec(int baseCustomers, int dayDivisor, int customerCap, double dirtyMultiplier,
            double duration, double spawnWindow, int maxConcurrent, int purchaseMin, int purchaseMax)
        {
            BaseCustomers = Math.Max(0, baseCustomers);
            DayDivisor = Math.Max(1, dayDivisor);
            CustomerCap = Math.Max(0, customerCap);
            DirtyMultiplier = Clamp01(dirtyMultiplier);
            Duration = Math.Max(0.0, duration);
            SpawnWindow = Clamp01(spawnWindow);
            MaxConcurrent = Math.Max(1, maxConcurrent);
            PurchaseMin = Math.Max(1, purchaseMin);
            PurchaseMax = Math.Max(PurchaseMin, purchaseMax);
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }

    /// <summary>
    /// 장사 규칙 (GDD 4-5, 노션 GDD 12.4 SalesSimulator). 엔진 의존 없음 — 실시간 장사(3-2) · 건너뛰기 · 측정 봇(4-1)이 같은 함수를 쓴다.
    /// </summary>
    public static class SalesRules
    {
        /// <summary>
        /// 손님 수 = 반올림(min(상한, 기본 + 일차 ÷ 나눔수(내림)) × (1 + 보너스 합) × (청결 0% 배율 + (1 − 배율) × 청결도)).
        /// 반올림은 0.5 올림(`Math.Floor(x + 0.5)`, 짝수 반올림 아님 — BALANCE 3-4). 보너스 합 = 스킬 · 매장 효과(4-3), 지금은 0.
        /// </summary>
        public static int CustomerCount(int day, double bonusSum, double cleanliness, SalesSpec spec)
        {
            int baseCount = Math.Min(spec.CustomerCap, spec.BaseCustomers + Math.Max(0, day) / spec.DayDivisor);
            double clean = cleanliness < 0 ? 0 : cleanliness > 1 ? 1 : cleanliness;
            double x = baseCount * Math.Max(0.0, 1.0 + bonusSum) * (spec.DirtyMultiplier + (1.0 - spec.DirtyMultiplier) * clean);
            return (int)Math.Floor(x + 0.5 + 1e-9);
        }

        /// <summary>k번째(0부터) 손님이 문에서 생기는 시각 = k × (장사 시간 × 생성 구간 ÷ 손님 수) (초).</summary>
        public static double SpawnTime(int k, int customerCount, SalesSpec spec)
        {
            if (customerCount <= 0)
                return 0;
            return k * (spec.Duration * spec.SpawnWindow / customerCount);
        }

        /// <summary>
        /// 손님 종류 고르기 (노션 GDD 12.4 SalesSimulator 5): 등장 일차(unlockDay)가 지난 종류 중 등장 가중치로 고른다.
        /// 고를 수 있는 종류가 없으면(가중치 합 0) −1.
        /// </summary>
        public static int PickCustomerType(IReadOnlyList<double> weights, IReadOnlyList<int> unlockDays, int day, IRandomSource random)
        {
            double total = 0;
            for (int i = 0; i < weights.Count; i++)
                if (unlockDays[i] <= day && weights[i] > 0)
                    total += weights[i];
            if (total <= 0)
                return -1;
            double pick = random.NextDouble() * total;
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (unlockDays[i] > day || weights[i] <= 0)
                    continue;
                last = i;
                if (pick < weights[i])
                    return i;
                pick -= weights[i];
            }
            return last; // 부동소수 끝자리 오차
        }

        /// <summary>손님 한 명이 사려는 개수 (최소 ~ 최대, 균등).</summary>
        public static int WantedCount(IRandomSource random, SalesSpec spec) => random.NextInt(spec.PurchaseMin, spec.PurchaseMax + 1);

        /// <summary>
        /// 진열된 것 하나를 고른다: 모든 진열대에 놓인 개수 전체 중 하나를 균등하게 → 작물이 뽑힐 확률은 진열 재고에 비례 (가중 무작위).
        /// 진열이 비었으면 false.
        /// </summary>
        public static bool TryPickUnit(IReadOnlyList<ShelfState> shelves, IRandomSource random, out int shelf, out int slot)
        {
            shelf = -1;
            slot = -1;
            int total = 0;
            for (int s = 0; s < shelves.Count; s++)
                total += shelves[s].TotalCount;
            if (total <= 0)
                return false;
            int pick = random.NextInt(0, total);
            for (int s = 0; s < shelves.Count; s++)
            {
                var state = shelves[s];
                for (int i = 0; i < state.SlotCount; i++)
                {
                    int count = state[i].Count;
                    if (pick < count)
                    {
                        shelf = s;
                        slot = i;
                        return true;
                    }
                    pick -= count;
                }
            }
            return false;
        }

        /// <summary>
        /// 손님 한 명의 장바구니: 사려는 개수만큼 매번 재고 비례로 골라 진열대에서 뺀다. 진열이 비면 거기서 멈춘다(0개면 빈손 퇴장).
        /// take = 진열대에서 실제로 빼는 일(겉모습 갱신 포함, 뺀 아이템 id) — 비우면 ShelfState.TakeOne.
        /// </summary>
        public static List<string> FillBasket(IReadOnlyList<ShelfState> shelves, IRandomSource random, SalesSpec spec, Func<int, int, string> take = null)
        {
            int wanted = WantedCount(random, spec);
            var basket = new List<string>(wanted);
            for (int n = 0; n < wanted; n++)
            {
                if (!TryPickUnit(shelves, random, out int shelf, out int slot))
                    break;
                string id = take != null ? take(shelf, slot) : shelves[shelf].TakeOne(slot);
                if (id != null)
                    basket.Add(id);
            }
            return basket;
        }
    }
}
