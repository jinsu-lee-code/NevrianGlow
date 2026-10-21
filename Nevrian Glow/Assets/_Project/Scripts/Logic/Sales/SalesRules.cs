using System;
using System.Collections.Generic;
using NevrianGlowFarm.Logic.Shop;

namespace NevrianGlowFarm.Logic.Sales
{
    /// <summary>
    /// 장사 규칙 값 (데이터 = Data/Stats/SalesSettings → SalesSettingsData.ToSpec). 손님 수는 입소문 모델(CustomerModel)이 정한다 (P-2, 2026-10-21).
    /// </summary>
    public readonly struct SalesSpec
    {
        public double Duration { get; }
        public double SpawnWindow { get; }
        public int MaxConcurrent { get; }
        public int PurchaseMin { get; }
        public int PurchaseMax { get; }
        /// <summary>흐트러진 진열 칸의 구매 확률 배율 (가게 정리 P-3, 기획 disorder_buy_mult 0.5).</summary>
        public double DisorderBuyMultiplier { get; }

        public SalesSpec(double duration, double spawnWindow, int maxConcurrent, int purchaseMin, int purchaseMax, double disorderBuyMultiplier = 1.0)
        {
            DisorderBuyMultiplier = Clamp01(disorderBuyMultiplier);
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
        /// 단골의 장바구니 (기획 extra_buy_chance): extraChance 확률로 1개 더, 최대 extraMax개 (손님마다 따로).
        /// extraChance가 0이면 난수를 쓰지 않는다 (스킬 없는 날의 장사 결과가 예전과 같게).
        /// </summary>
        public static int WithExtra(int wanted, double extraChance, int extraMax, IRandomSource random)
        {
            if (extraChance <= 0 || wanted >= extraMax || random.NextDouble() >= extraChance)
                return wanted;
            return wanted + 1;
        }

        /// <summary>
        /// 진열된 것 하나를 고른다: 모든 진열대에 놓인 개수 전체 중 하나를 균등하게 → 작물이 뽑힐 확률은 진열 재고에 비례 (가중 무작위).
        /// 진열이 비었으면 false.
        /// </summary>
        public static bool TryPickUnit(IReadOnlyList<ShelfState> shelves, IRandomSource random, out int shelf, out int slot, double disorderMultiplier = 1.0)
        {
            shelf = -1;
            slot = -1;
            // 흐트러진 칸이 있으면 그 칸의 무게만 배율만큼 (구매 확률 절반 — 청소 v2). 없으면 예전과 같은 난수 흐름.
            if (disorderMultiplier < 1.0 && AnyDisordered(shelves))
                return TryPickWeighted(shelves, random, disorderMultiplier, out shelf, out slot);
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

        private static bool AnyDisordered(IReadOnlyList<ShelfState> shelves)
        {
            foreach (var s in shelves)
                if (s.DisorderedCount > 0)
                    return true;
            return false;
        }

        // 개수 × (흐트러진 칸이면 배율) 무게로 한 개 고르기
        private static bool TryPickWeighted(IReadOnlyList<ShelfState> shelves, IRandomSource random, double multiplier, out int shelf, out int slot)
        {
            shelf = -1;
            slot = -1;
            double total = 0;
            foreach (var s in shelves)
                for (int i = 0; i < s.SlotCount; i++)
                    total += s[i].Count * (s.IsDisordered(i) ? multiplier : 1.0);
            if (total <= 0)
                return false;
            double pick = random.NextDouble() * total;
            for (int sh = 0; sh < shelves.Count; sh++)
            {
                var state = shelves[sh];
                for (int i = 0; i < state.SlotCount; i++)
                {
                    double w = state[i].Count * (state.IsDisordered(i) ? multiplier : 1.0);
                    if (w <= 0)
                        continue;
                    shelf = sh;
                    slot = i;
                    if (pick < w)
                        return true;
                    pick -= w;
                }
            }
            return shelf >= 0; // 부동소수 끝자리 오차 → 마지막 칸
        }

        /// <summary>
        /// 손님 한 명의 장바구니: 사려는 개수만큼 매번 재고 비례로 골라 진열대에서 뺀다. 진열이 비면 거기서 멈춘다(0개면 빈손 퇴장).
        /// take = 진열대에서 실제로 빼는 일(겉모습 갱신 포함, 뺀 아이템 id) — 비우면 ShelfState.TakeOne.
        /// </summary>
        public static List<string> FillBasket(IReadOnlyList<ShelfState> shelves, IRandomSource random, SalesSpec spec, Func<int, int, string> take = null) =>
            FillBasket(shelves, random, WantedCount(random, spec), take, spec.DisorderBuyMultiplier);

        /// <summary>사려는 개수를 정해 둔 장바구니 (1일차 첫 손님 고정 · 단골의 장바구니 — SalesSession.NextWanted).</summary>
        public static List<string> FillBasket(IReadOnlyList<ShelfState> shelves, IRandomSource random, int wanted, Func<int, int, string> take = null, double disorderMultiplier = 1.0)
        {
            wanted = Math.Max(0, wanted);
            var basket = new List<string>(wanted);
            for (int n = 0; n < wanted; n++)
            {
                if (!TryPickUnit(shelves, random, out int shelf, out int slot, disorderMultiplier))
                    break;
                string id = take != null ? take(shelf, slot) : shelves[shelf].TakeOne(slot);
                if (id != null)
                    basket.Add(id);
            }
            return basket;
        }
    }
}
