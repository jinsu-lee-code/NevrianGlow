namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 도구 공통 (괭이 · 물뿌리개 · 대걸레, BALANCE 2-11). 칸당 1개.
    /// 등급은 에셋 하나씩이다 (tool_hoe_1 · tool_hoe_2 …) — 등급 추가는 에셋 추가만으로.
    /// </summary>
    public abstract class ToolData : ItemData
    {
        public override int MaxStack => 1;
    }
}
