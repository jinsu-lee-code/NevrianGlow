using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 비료 아이템 (GDD 4-9, BALANCE 2-12). 들고 심은 칸에 좌클릭하면 그 작물이 준 순간 이 날 수만큼 자란다 — 작물당 1회 (기획 shop.csv 10/30).
    /// 비료 종류를 늘리려면 에셋만 추가 (자라는 날 수가 다른 비료 등).
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Fertilizer", fileName = "item_fertilizer_new")]
    public class FertilizerData : ItemData
    {
        [Header("비료")]
        [Tooltip("준 순간 자라는 날 수 — 다 자란 날을 넘지 않는다 (일, 10/30 전에는 줄이는 성장일)")]
        [SerializeField, Min(1)] private int growDaysReduction = 1;

        public int GrowDaysReduction => growDaysReduction;
    }
}
