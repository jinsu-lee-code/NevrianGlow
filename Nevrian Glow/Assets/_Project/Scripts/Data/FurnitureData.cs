using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>진열 가구 종류 = 놓을 수 있는 가구 자리 종류. enum은 끝에만 추가.</summary>
    public enum FurnitureKind
    {
        /// <summary>과일 진열대 — 매장 가운데 자리, 수확한 작물만.</summary>
        FruitShelf,
        /// <summary>일반 진열대 — 벽 쪽 자리, 가공품만 (2차).</summary>
        GoodsShelf,
    }

    /// <summary>
    /// 진열 가구 하나 = 에셋 하나 (TASKS P-4, 레벨배치 설정안 2절 · 최종 기획서 7-1). 기획 items.csv의 furniture 행.
    /// 과일 진열대 = 1층 낮은 판매대 · 한 대에 한 종류 · 작물 30개. 칸(층) 수 · 칸당 개수는 진열대(ShelfDisplay)가 이 값으로 정한다.
    /// 가구는 정해진 가구 자리(FurnitureSpot, 같은 종류)에만 놓인다. 추가 구매 · 옮기기는 P1.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Furniture", fileName = "furn_new")]
    public class FurnitureData : ItemData
    {
        [Header("가구")]
        [Tooltip("놓을 수 있는 가구 자리 종류")]
        [SerializeField] private FurnitureKind kind = FurnitureKind.FruitShelf;
        [Tooltip("층(칸) 수 — 층마다 한 종류 (과일 진열대 1, 일반 진열대 3)")]
        [SerializeField, Min(1)] private int layers = 1;
        [Tooltip("층마다 놓는 개수 (개, 과일 진열대 30 — 기획 items.csv 작물 display_per_slot)")]
        [SerializeField, Min(1)] private int itemsPerLayer = 30;

        public FurnitureKind Kind => kind;
        public int Layers => layers;
        public int ItemsPerLayer => itemsPerLayer;
    }
}
