using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>가방 확장 (shop_bag_expand). 한 단계에 가방 slots칸.</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Shop/Bag Expand Product", fileName = "shop_bag_expand")]
    public class BagExpandProductData : ShopProductData
    {
        [Header("효과")]
        [Tooltip("한 단계에 늘어나는 가방 칸 (칸)")]
        [SerializeField, Min(1)] private int slots = 4;

        public int Slots => slots;
    }
}
