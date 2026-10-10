using NevrianGlowFarm.Data;

namespace NevrianGlowFarm.Systems.Interaction
{
    /// <summary>
    /// 좌클릭(든 아이템 사용)을 받는 대상 — 괭이로 갈기 · 씨앗 심기 · 물주기 등 (GDD 4-3).
    /// 대상이 든 아이템의 에셋(또는 종류)을 보고 할 일을 고른다. 특정 id로 분기하지 않는다.
    /// </summary>
    public interface IItemUseTarget
    {
        /// <summary>든 아이템으로 할 수 있는 일을 짧게 (예: "괭이로 갈기"). 없으면 null 또는 빈 문자열. held는 빈 칸이면 null.</summary>
        string GetUsePrompt(ItemData held);

        /// <summary>좌클릭. GetUsePrompt가 비어 있으면 호출되지 않는다.</summary>
        void Use(ItemData held);
    }
}
