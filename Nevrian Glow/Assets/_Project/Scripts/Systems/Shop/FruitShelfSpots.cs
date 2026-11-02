using System.Collections.Generic;
using NevrianGlowFarm.Data;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Shop
{
    /// <summary>
    /// 가게 과일 진열대 자리 묶음 (기획 shop_layout.json 11/02). 진열대는 Build Shop Layout이 모든 자리에 미리 놓고,
    /// 여기서 자리의 열리는 단계(FurnitureSpot.UnlockStage) ≤ 추가로 산 진열대 수인 자리만 켠다 — 0 = 시작 진열대.
    /// 산 수는 상점 구매 단계(ShopSystem, 저장됨)에서 온다. 상점은 다른 씬(Core)이라 ShopSystem이 이쪽을 부른다.
    /// </summary>
    public class FruitShelfSpots : MonoBehaviour
    {
        [Header("디버그")]
        [Tooltip("추가로 연 진열대 수 (읽기 전용)")]
        [SerializeField] private int opened;

        private static FruitShelfSpots instance;
        private readonly List<FurnitureSpot> spots = new List<FurnitureSpot>();

        public static FruitShelfSpots Instance => instance;
        public int Opened => opened;

        /// <summary>추가로 열 수 있는 최대 진열대 수 (가장 늦게 열리는 자리의 단계).</summary>
        public int Capacity
        {
            get
            {
                int max = 0;
                foreach (var s in spots)
                    max = Mathf.Max(max, s.UnlockStage);
                return max;
            }
        }

        /// <summary>지금 켜진 진열대 수.</summary>
        public int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (var s in spots)
                    if (s.UnlockStage <= opened && ShelfOf(s) != null)
                        n++;
                return n;
            }
        }

        private void Awake()
        {
            instance = this;
            foreach (var s in GetComponentsInChildren<FurnitureSpot>(true))
                if (s.Kind == FurnitureKind.FruitShelf)
                    spots.Add(s);
        }

        private void Start()
        {
            // Core가 먼저 떠 있으면 지금 산 수로 맞춘다 (이후 구매 · 불러오기는 ShopSystem이 부른다)
            if (ShopSystem.Instance != null)
                SetOpened(ShopSystem.Instance.FruitShelvesBought);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        /// <summary>추가로 산 진열대 수만큼 자리를 켠다 (안 산 자리의 진열대는 끔).</summary>
        public void SetOpened(int count)
        {
            opened = Mathf.Max(0, count);
            foreach (var s in spots)
            {
                var shelf = ShelfOf(s);
                if (shelf != null)
                    shelf.gameObject.SetActive(s.UnlockStage <= opened);
            }
        }

        private static ShelfDisplay ShelfOf(FurnitureSpot spot) => spot.GetComponentInChildren<ShelfDisplay>(true);
    }
}
