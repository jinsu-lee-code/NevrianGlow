using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 도구 공통 (괭이 · 물뿌리개 · 대걸레, BALANCE 2-11). 칸당 1개.
    /// 등급은 에셋 하나씩이다 (tool_hoe_1 · tool_hoe_2 …) — 등급 추가는 에셋 추가만으로.
    /// </summary>
    public abstract class ToolData : ItemData
    {
        [Header("동작")]
        [Tooltip("좌클릭 후 효과가 들어가기까지 — 동작(기울이기 등)이 끝나는 시점, 그동안은 다시 쓸 수 없다 (초)")]
        [SerializeField, Min(0f)] private float useDelay;

        public override int MaxStack => 1;
        public float UseDelay => useDelay;
    }
}
