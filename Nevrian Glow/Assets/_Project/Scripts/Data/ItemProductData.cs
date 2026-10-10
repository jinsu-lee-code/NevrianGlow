using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>아이템을 주는 상품 (씨앗 · 소모품). 인벤토리(핫바 → 가방)에 들어간다.</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Shop/Item Product", fileName = "shop_item_new")]
    public class ItemProductData : ShopProductData
    {
        [Header("효과")]
        [Tooltip("주는 아이템")]
        [SerializeField] private ItemData item;
        [Tooltip("한 번 살 때 주는 개수 (개)")]
        [SerializeField, Min(1)] private int count = 1;

        public ItemData Item => item;
        public int Count => count;
    }
}
