using UnityEngine;
using UnityEngine.Serialization;

namespace NevrianGlowFarm.Data
{
    /// <summary>핫바 · 가방 · 창고 · 진열 (BALANCE 2-4). 칸당 개수는 아이템마다(ItemData), 확장 가격은 상점 상품(3-3)에 둔다.</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Settings/Inventory", fileName = "InventorySettings")]
    public class InventorySettingsData : ScriptableObject
    {
        [Header("핫바")]
        [Tooltip("처음 핫바 칸 수 (칸)")]
        [SerializeField, Min(1)] private int hotbarBaseSlots = 1;
        [Tooltip("스킬로 늘릴 수 있는 최대 핫바 칸 수 — 숫자 키 1~8 (칸)")]
        [SerializeField, Range(1, 8)] private int hotbarMaxSlots = 1;

        [Header("가방")]
        [Tooltip("처음 가방 칸 수 (칸)")]
        [FormerlySerializedAs("baseSlots")]
        [SerializeField, Min(0)] private int bagBaseSlots;
        [Tooltip("가방 확장 1단계마다 늘어나는 칸 수 — 단계 가격은 상점 상품(3-3) (칸)")]
        [FormerlySerializedAs("slotsPerExpand")]
        [SerializeField, Min(1)] private int bagSlotsPerExpand = 1;
        [Tooltip("최대 가방 칸 수 (칸)")]
        [FormerlySerializedAs("maxSlots")]
        [SerializeField, Min(0)] private int bagMaxSlots;

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

        public int HotbarBaseSlots => Mathf.Min(hotbarBaseSlots, hotbarMaxSlots);
        public int HotbarMaxSlots => hotbarMaxSlots;
        public int BagBaseSlots => Mathf.Min(bagBaseSlots, bagMaxSlots);
        public int BagSlotsPerExpand => bagSlotsPerExpand;
        public int BagMaxSlots => bagMaxSlots;
        public float StorageAccessRadius => storageAccessRadius;
        public int ShelfBaseSlots => shelfBaseSlots;
        public int ShelfItemsPerSlot => shelfItemsPerSlot;
        public int ShelfSlotsPerUpgrade => shelfSlotsPerUpgrade;
        public float ShelfSnapDistance => shelfSnapDistance;
        public float PlaceDuration => placeDuration;
    }
}
