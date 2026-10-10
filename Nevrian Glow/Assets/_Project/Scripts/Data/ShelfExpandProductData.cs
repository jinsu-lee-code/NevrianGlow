using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>진열대 확장 (shop_shelf_expand). 한 단계에 진열 칸 slots칸.</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Shop/Shelf Expand Product", fileName = "shop_shelf_expand")]
    public class ShelfExpandProductData : ShopProductData
    {
        [Header("효과")]
        [Tooltip("한 단계에 늘어나는 진열 칸 (칸)")]
        [SerializeField, Min(1)] private int slots = 2;

        public int Slots => slots;
    }
}
