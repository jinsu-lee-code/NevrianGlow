using NevrianGlowFarm.Data;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Interaction
{
    /// <summary>
    /// 조준한 "점"이 필요한 대상 (진열대: 어느 칸을 보는지 → 홀로그램 · 프롬프트). Raycaster가 보는 동안 매 프레임 알려 준다.
    /// </summary>
    public interface IAimPointTarget
    {
        /// <summary>조준점 · 든 아이템이 바뀌었을 수 있다. 프롬프트를 다시 받아야 하면 true (예: 다른 칸으로 옮김).</summary>
        bool UpdateAim(Vector3 point, ItemData held);

        /// <summary>더 이상 보지 않는다 (다른 대상 · 허공 · 화면 열림). 홀로그램을 숨긴다.</summary>
        void ClearAim();
    }

    /// <summary>우클릭(보조 사용)을 받는 대상 — 진열대 1개 회수 (GDD 4-3).</summary>
    public interface ISecondaryUseTarget
    {
        /// <summary>우클릭으로 할 수 있는 일을 짧게. 없으면 null 또는 빈 문자열. 키 이름은 붙이지 않는다.</summary>
        string GetSecondaryPrompt(ItemData held);

        /// <summary>우클릭. GetSecondaryPrompt가 비어 있으면 호출되지 않는다.</summary>
        void UseSecondary(ItemData held);
    }
}
