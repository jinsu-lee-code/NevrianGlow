using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>밭 구획 확장 (shop_plot_expand). 한 단계에 구획 plots개.</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Shop/Plot Expand Product", fileName = "shop_plot_expand")]
    public class PlotExpandProductData : ShopProductData
    {
        [Header("효과")]
        [Tooltip("한 단계에 늘어나는 구획 (구획)")]
        [SerializeField, Min(1)] private int plots = 1;

        public int Plots => plots;
    }
}
