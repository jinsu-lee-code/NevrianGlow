using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>핫바 확장 (shop_hotbar_expand). 한 단계에 핫바 slots칸 — 스킬 '분주한 손' 대신 레벨 5 해금 상품 (수정사항 10/28).</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Shop/Hotbar Expand Product", fileName = "shop_hotbar_expand")]
    public class HotbarExpandProductData : ShopProductData
    {
        [Header("효과")]
        [Tooltip("한 단계에 늘어나는 핫바 칸 (칸)")]
        [SerializeField, Min(1)] private int slots = 1;

        public int Slots => slots;
    }
}
