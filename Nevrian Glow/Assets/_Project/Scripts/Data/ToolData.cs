using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 핫바 도구 한 종 (손 · 씨앗 주머니 · 물뿌리개 · 대걸레 · 비료 등, BALANCE 9-4).
    /// 상호작용 대상은 도구 에셋을 받아 프롬프트 · 동작을 정한다. 특정 id로 분기하지 말고 에셋 참조로 비교한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Tool", fileName = "tool_new")]
    public class ToolData : ContentData
    {
    }
}
