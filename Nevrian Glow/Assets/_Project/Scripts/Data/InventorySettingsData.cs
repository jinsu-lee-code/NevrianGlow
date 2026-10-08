using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>인벤토리 · 창고 · 진열 (BALANCE 2-4). 확장 가격은 상점 상품(3-3)에 둔다.</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Settings/Inventory", fileName = "InventorySettings")]
    public class InventorySettingsData : ScriptableObject
    {
        [Header("인벤토리")]
        [Tooltip("처음 칸 수 (칸)")]
        [SerializeField, Min(1)] private int baseSlots = 1;
        [Tooltip("확장 1회마다 늘어나는 칸 수 (칸)")]
        [SerializeField, Min(1)] private int slotsPerExpand = 1;
        [Tooltip("최대 칸 수 (칸)")]
        [SerializeField, Min(1)] private int maxSlots = 1;
        [Tooltip("한 칸에 쌓을 수 있는 개수 (개)")]
        [SerializeField, Min(1)] private int stackSize = 1;

        [Header("창고")]
        [Tooltip("창고 접근 반경 (m)")]
        [SerializeField, Min(0.1f)] private float storageAccessRadius = 0.1f;

        [Header("진열대")]
        [Tooltip("처음 진열 칸 수 (칸)")]
        [SerializeField, Min(1)] private int shelfBaseSlots = 1;
        [Tooltip("진열 칸 하나에 놓는 개수 (개)")]
        [SerializeField, Min(1)] private int shelfItemsPerSlot = 1;
        [Tooltip("업그레이드 1회마다 늘어나는 진열 칸 (칸)")]
        [SerializeField, Min(1)] private int shelfSlotsPerUpgrade = 1;
        [Tooltip("홀로그램이 칸에 달라붙는 거리 (m)")]
        [SerializeField, Min(0f)] private float shelfSnapDistance;
        [Tooltip("진열 놓기 동작 시간 (초)")]
        [SerializeField, Min(0f)] private float placeDuration;

        public int BaseSlots => baseSlots;
        public int SlotsPerExpand => slotsPerExpand;
        public int MaxSlots => maxSlots;
        public int StackSize => stackSize;
        public float StorageAccessRadius => storageAccessRadius;
        public int ShelfBaseSlots => shelfBaseSlots;
        public int ShelfItemsPerSlot => shelfItemsPerSlot;
        public int ShelfSlotsPerUpgrade => shelfSlotsPerUpgrade;
        public float ShelfSnapDistance => shelfSnapDistance;
        public float PlaceDuration => placeDuration;
    }
}
