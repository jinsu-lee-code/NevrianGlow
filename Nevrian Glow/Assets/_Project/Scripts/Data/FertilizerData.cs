using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 비료 아이템 (GDD 4-9, BALANCE 2-12). 들고 심은 칸에 좌클릭하면 그 작물의 필요 성장일이 줄어든다 — 작물당 1회, 그날 밤부터.
    /// 비료 종류를 늘리려면 에셋만 추가 (줄이는 날 수가 다른 비료 등).
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Fertilizer", fileName = "item_fertilizer_new")]
    public class FertilizerData : ItemData
    {
        [Header("비료")]
        [Tooltip("줄이는 필요 성장일 — 최소 1일은 남는다 (일)")]
        [SerializeField, Min(1)] private int growDaysReduction = 1;

        public int GrowDaysReduction => growDaysReduction;
    }
}
