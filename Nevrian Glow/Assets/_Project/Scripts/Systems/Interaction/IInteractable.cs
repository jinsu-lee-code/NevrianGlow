using NevrianGlowFarm.Data;

namespace NevrianGlowFarm.Systems.Interaction
{
    /// <summary>
    /// 화면 중앙 Raycast로 고를 수 있는 대상 (노션 GDD 12.3). Interactable 레이어의 콜라이더와 같은 오브젝트(또는 부모)에 둔다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>현재 도구로 할 수 있는 일을 한 줄로. 할 수 있는 일이 없으면 null 또는 빈 문자열.</summary>
        string GetPrompt(ToolData tool);

        /// <summary>클릭 또는 E. GetPrompt가 비어 있으면 호출되지 않는다.</summary>
        void Interact(ToolData tool);
    }
}
