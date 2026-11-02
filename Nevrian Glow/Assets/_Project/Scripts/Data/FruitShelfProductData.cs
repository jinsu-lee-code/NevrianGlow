using System.Collections.Generic;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 과일 진열대 추가 (shop_fruit_shelf, 기획 shop.csv · shop_layout.json 11/02). 단계마다 가게의 다음 빈 자리에 진열대를 놓는다.
    /// 어느 자리 · 어떤 진열대(계단형 · 판매대)인지는 가게 배치(FruitShelfSpots · FurnitureSpot 열리는 단계)가 정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Shop/Fruit Shelf Product", fileName = "shop_fruit_shelf")]
    public class FruitShelfProductData : ShopProductData
    {
        [Header("효과")]
        [Tooltip("한 단계에 놓는 진열대 — 단계별 목록이 비어 있을 때 (대)")]
        [SerializeField, Min(1)] private int shelves = 1;
        [Tooltip("단계별로 놓는 진열대 (1단계 = 첫 칸, 대). 비우면 모든 단계가 위의 값 — 기획 grant_value_per_step")]
        [SerializeField] private List<int> shelvesPerTier = new List<int>();

        /// <summary>tier 단계(0부터)에서 놓는 진열대 수.</summary>
        public int ShelvesAt(int tier)
        {
            if (shelvesPerTier.Count == 0)
                return shelves;
            return Mathf.Max(1, shelvesPerTier[Mathf.Clamp(tier, 0, shelvesPerTier.Count - 1)]);
        }

        /// <summary>앞에서부터 tiersBought단계를 샀을 때 늘어난 진열대 수.</summary>
        public int ShelvesUpTo(int tiersBought)
        {
            int total = 0;
            for (int i = 0; i < tiersBought; i++)
                total += ShelvesAt(i);
            return total;
        }
    }
}
