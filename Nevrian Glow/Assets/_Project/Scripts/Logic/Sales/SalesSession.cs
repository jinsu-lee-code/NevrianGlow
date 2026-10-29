using System;
using System.Collections.Generic;
using NevrianGlowFarm.Logic.Shop;

namespace NevrianGlowFarm.Logic.Sales
{
    /// <summary>
    /// 하루 장사 한 번 (노션 GDD 12.4 SalesSimulator 1 · 2 · 4 · 7). 엔진 의존 없음.
    /// 실시간(3-2): SpawnDue로 생길 손님 수를 받아 손님을 만들고, 손님이 진열대에서 FillBasket → 계산 Pay → 나가면 Leave.
    /// 건너뛰기: SkipRemaining이 아직 안 들어온 손님 전원을 같은 함수(FillBasket · Pay)로 즉시 처리 — 같은 시드면 실시간과 결과가 같다.
    /// 가게 안 손님(이미 장바구니를 채운 손님)의 계산은 어댑터가 Pay · Leave로 마무리한다.
    /// </summary>
    public sealed class SalesSession
    {
        private readonly SalesSpec spec;
        private readonly Dictionary<string, int> sold = new Dictionary<string, int>();
        private readonly int firstWanted;
        private readonly double extraChance;
        private readonly int extraMax;
        private readonly int everyWanted;
        private int baskets;

        /// <param name="firstWanted">첫 손님이 사는 개수 고정 (1일차 첫 손님 2개, 0 = 무작위)</param>
        /// <param name="extraChance">손님마다 1개 더 살 확률 (단골의 장바구니)</param>
        /// <param name="extraMax">1개 더 사도 넘지 않는 개수</param>
        /// <param name="everyWanted">모든 손님이 사려는 개수 고정 (튜토리얼 고정값 "진열된 것 모두 구매" — 진열 수를 넘기면 다 산다, 0 = 무작위)</param>
        public SalesSession(int customerCount, SalesSpec spec, int firstWanted = 0, double extraChance = 0, int extraMax = 0, int everyWanted = 0)
        {
            this.spec = spec;
            Total = Math.Max(0, customerCount);
            this.firstWanted = Math.Max(0, firstWanted);
            this.extraChance = Math.Max(0, extraChance);
            this.extraMax = Math.Max(spec.PurchaseMax, extraMax);
            this.everyWanted = Math.Max(0, everyWanted);
        }

        /// <summary>다음 손님이 사려는 개수 (손님 하나에 한 번 — 장바구니를 채울 때 부른다).</summary>
        public int NextWanted(IRandomSource random)
        {
            baskets++;
            if (baskets == 1 && firstWanted > 0)
                return firstWanted;
            if (everyWanted > 0)
                return everyWanted;
            return SalesRules.WithExtra(SalesRules.WantedCount(random, spec), extraChance, extraMax, random);
        }

        public SalesSpec Spec => spec;
        /// <summary>오늘 올 손님 수.</summary>
        public int Total { get; }
        /// <summary>문에서 생긴 손님 수 (건너뛰기로 처리된 손님 포함).</summary>
        public int Spawned { get; private set; }
        /// <summary>지금 가게 안 손님 수.</summary>
        public int InStore { get; private set; }
        /// <summary>1개 이상 사고 계산한 손님.</summary>
        public int Buyers { get; private set; }
        /// <summary>진열이 비어 빈손으로 나간 손님.</summary>
        public int EmptyHanded { get; private set; }
        public int ItemsSold { get; private set; }
        public int Revenue { get; private set; }
        /// <summary>아이템 id → 판매 개수.</summary>
        public IReadOnlyDictionary<string, int> Sold => sold;
        /// <summary>모든 손님이 들어왔다 나갔다.</summary>
        public bool IsFinished => Spawned >= Total && InStore == 0;

        /// <summary>다음 손님이 생길 시각 (초). 다 들어왔으면 무한대.</summary>
        public double NextSpawnTime => Spawned < Total ? SalesRules.SpawnTime(Spawned, Total, spec) : double.PositiveInfinity;

        /// <summary>
        /// elapsed초 시점에 새로 들어올 손님 수. 동시 최대 손님을 넘으면 자리가 날 때까지 미룬다(늦게 들어옴).
        /// </summary>
        public int SpawnDue(double elapsed)
        {
            int count = 0;
            while (Spawned < Total && InStore < spec.MaxConcurrent && SalesRules.SpawnTime(Spawned, Total, spec) <= elapsed)
            {
                Spawned++;
                InStore++;
                count++;
            }
            return count;
        }

        /// <summary>손님 하나가 계산한다 (장바구니가 비었으면 빈손 퇴장으로 센다). 매출은 계산 때 더한다(GDD 4-5).</summary>
        public int Pay(IReadOnlyList<string> basket, Func<string, int> priceOf)
        {
            if (basket == null || basket.Count == 0)
            {
                EmptyHanded++;
                return 0;
            }
            int total = 0;
            foreach (var id in basket)
            {
                total += Math.Max(0, priceOf(id));
                sold[id] = sold.TryGetValue(id, out int n) ? n + 1 : 1;
            }
            Buyers++;
            ItemsSold += basket.Count;
            Revenue += total;
            return total;
        }

        /// <summary>손님 하나가 가게를 나갔다.</summary>
        public void Leave()
        {
            if (InStore > 0)
                InStore--;
        }

        /// <summary>
        /// 건너뛰기 · 장사 시간 끝: 아직 안 들어온 손님 전원이 순서대로 장바구니를 채우고 계산한다.
        /// 가게 안 손님은 어댑터가 먼저 Pay · Leave로 마무리한다. 처리한 손님 수를 돌려준다.
        /// </summary>
        public int SkipRemaining(IReadOnlyList<ShelfState> shelves, IRandomSource random, Func<string, int> priceOf, Func<int, int, string> take = null)
        {
            int handled = 0;
            while (Spawned < Total)
            {
                Spawned++;
                Pay(SalesRules.FillBasket(shelves, random, NextWanted(random), take, spec.DisorderBuyMultiplier), priceOf);
                handled++;
            }
            return handled;
        }
    }
}
