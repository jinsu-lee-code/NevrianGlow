using System.Collections.Generic;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 도구 등급 상품 (괭이 · 대걸레 · 물뿌리개, BALANCE 2-11). 단계 i를 사면 등급 도구 tierTools[i]를 받고,
    /// 인벤토리 · 창고에 있던 같은 계열 도구(baseTool 또는 앞 단계)는 새 도구로 바뀐다 (TASKS 3-3 "구매 시 인벤토리의 도구 교체").
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Shop/Tool Upgrade Product", fileName = "shop_tool_upgrade_new")]
    public class ToolUpgradeProductData : ShopProductData
    {
        [Header("효과")]
        [Tooltip("처음 가진 등급 (교체 대상)")]
        [SerializeField] private ToolData baseTool;
        [Tooltip("단계별로 받는 등급 도구 — 단계 수와 같게")]
        [SerializeField] private List<ToolData> tierTools = new List<ToolData>();

        public ToolData BaseTool => baseTool;
        public IReadOnlyList<ToolData> TierTools => tierTools;
        public ToolData ToolAt(int tier) => tier >= 0 && tier < tierTools.Count ? tierTools[tier] : null;

        /// <summary>교체 대상 = 처음 등급 + 앞 단계 등급들.</summary>
        public IEnumerable<ToolData> ReplaceableBefore(int tier)
        {
            if (baseTool != null)
                yield return baseTool;
            for (int i = 0; i < tier && i < tierTools.Count; i++)
                if (tierTools[i] != null)
                    yield return tierTools[i];
        }

        public override string TierLabel(int tier)
        {
            var tool = ToolAt(tier);
            return tool != null ? tool.DisplayName : base.TierLabel(tier);
        }

        public override Sprite IconFor(int tier)
        {
            var own = base.IconFor(tier);
            if (own != null)
                return own;
            var tool = ToolAt(tier);
            return tool != null ? tool.Icon : null;
        }
    }
}
