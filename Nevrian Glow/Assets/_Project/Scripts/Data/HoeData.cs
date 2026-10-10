using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>괭이 한 등급 (BALANCE 2-11). 좌클릭 1회로 칸의 개간 작업량을 이만큼 채운다.</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Tool/Hoe", fileName = "tool_hoe_new")]
    public class HoeData : ToolData
    {
        [Header("괭이")]
        [Tooltip("좌클릭 1회 개간 작업량 — 칸 필요 작업량 ÷ 이 값 = 개간 횟수 (작업량)")]
        [SerializeField, Min(0.01f)] private float workPerUse = 0.01f;

        public float WorkPerUse => workPerUse;
    }
}
