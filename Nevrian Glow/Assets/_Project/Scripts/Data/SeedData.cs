using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>씨앗 아이템 (BALANCE 2-12). 들고 개간된 칸에 좌클릭하면 이 작물을 심는다.</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Seed", fileName = "item_seed_new")]
    public class SeedData : ItemData
    {
        [Header("씨앗")]
        [Tooltip("심으면 자라는 작물")]
        [SerializeField] private CropData crop;

        public CropData Crop => crop;
    }
}
