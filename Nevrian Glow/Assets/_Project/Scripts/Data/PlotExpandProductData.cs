using System.Collections.Generic;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 밭 구획 확장 (shop_plot_expand). 단계마다 늘어나는 구획 수가 다를 수 있다 (기획 shop.csv grant_value_per_step: 16;32;64;112칸 → 1;2;4;7구획).
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Shop/Plot Expand Product", fileName = "shop_plot_expand")]
    public class PlotExpandProductData : ShopProductData
    {
        [Header("효과")]
        [Tooltip("한 단계에 늘어나는 구획 — 단계별 목록이 비어 있을 때 (구획)")]
        [SerializeField, Min(1)] private int plots = 1;
        [Tooltip("단계별로 늘어나는 구획 (1단계 = 첫 칸, 구획). 비우면 모든 단계가 위의 값")]
        [SerializeField] private List<int> plotsPerTier = new List<int>();

        /// <summary>tier 단계(0부터)에서 늘어나는 구획.</summary>
        public int PlotsAt(int tier)
        {
            if (plotsPerTier.Count == 0)
                return plots;
            return Mathf.Max(1, plotsPerTier[Mathf.Clamp(tier, 0, plotsPerTier.Count - 1)]);
        }
    }
}
