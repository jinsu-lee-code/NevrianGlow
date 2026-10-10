namespace NevrianGlowFarm.Systems.Interaction
{
    /// <summary>
    /// E(손 동작)로 다루는 대상 — 줍기 · 수확 · 창고 · 문 · 쓰레기통 등 (GDD 4-3). 든 아이템과 무관하다.
    /// 화면 중앙 Raycast로 고른다. Interactable 레이어의 콜라이더와 같은 오브젝트(또는 부모)에 둔다.
    /// </summary>
    public interface IHandInteractable
    {
        /// <summary>E로 할 수 있는 일을 짧게 (예: "수확"). 없으면 null 또는 빈 문자열. 키 이름은 붙이지 않는다.</summary>
        string GetHandPrompt();

        /// <summary>E. GetHandPrompt가 비어 있으면 호출되지 않는다.</summary>
        void InteractHand();
    }
}
