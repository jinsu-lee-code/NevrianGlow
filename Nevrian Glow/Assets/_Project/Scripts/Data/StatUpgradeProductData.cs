using NevrianGlowFarm.Logic.Game;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 능력치 단계 상품 (기획 shop.csv 10/30 새 상품 — 더 큰 장바구니 · 매장 홍보 · 수확물 품질 상승). 살 때마다 능력치 한 단계.
    /// 산 단계 수는 ShopState가 들고, StatSystem이 Rebuild할 때 변경으로 모은다 (ShopSystem.CollectModifiers).
    /// 단계마다 바뀌는 값 · 방식은 이 에셋에 둔다 — CSV 가져오기는 가격 · 단계만 바꾼다.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Shop/Stat Upgrade", fileName = "shop_new_upgrade")]
    public class StatUpgradeProductData : ShopProductData
    {
        [Header("능력치")]
        [Tooltip("단계마다 바꾸는 능력치 (Data/Stats/stat_*)")]
        [SerializeField] private StatData stat;
        [Tooltip("바꾸는 방식 — Add: 산 단계 × 값을 더함 · Multiply: (1 + 산 단계 × 값)을 곱함 (+10%씩이 겹쳐 곱해지지 않게)")]
        [SerializeField] private ModifierOp op = ModifierOp.Add;
        [Tooltip("한 단계에 바뀌는 값 (예: 장바구니 +1개, 손님 +0.2배, 판매가 +0.1배)")]
        [SerializeField] private float perStep = 1f;

        public StatData Stat => stat;
        public ModifierOp Op => op;
        public float PerStep => perStep;

        /// <summary>산 단계 수만큼의 변경 하나. 산 적이 없거나 능력치가 비면 false.</summary>
        public bool TryModifier(int steps, out StatModifier modifier)
        {
            modifier = default;
            if (stat == null || steps <= 0)
                return false;
            double value = op == ModifierOp.Multiply ? 1.0 + steps * (double)perStep : steps * (double)perStep;
            modifier = new StatModifier(stat.Id, op, value);
            return true;
        }
    }
}
