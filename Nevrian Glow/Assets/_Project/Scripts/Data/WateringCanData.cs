using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>물뿌리개 한 등급 (BALANCE 2-11). 칸 수에 따라 1칸 / 옆 칸 / 같은 밭 / 이웃 밭으로 넓어진다 (2-7).</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Tool/Watering Can", fileName = "tool_watering_new")]
    public class WateringCanData : ToolData
    {
        [Header("물뿌리개")]
        [Tooltip("한 번에 물을 주는 칸 수 — 1 · 2 · 4 · 8 (칸)")]
        [SerializeField, Min(1)] private int cellCount = 1;

        public int CellCount => cellCount;
    }
}
