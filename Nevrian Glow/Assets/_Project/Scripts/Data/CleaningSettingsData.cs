using System;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>청소 (BALANCE 2-5). 대걸레 반경 · 속도는 대걸레 등급 데이터(MopData, BALANCE 2-11)에 있다.</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Settings/Cleaning", fileName = "CleaningSettings")]
    public class CleaningSettingsData : ScriptableObject
    {
        /// <summary>일차에 따라 늘어나는 개수: 기본 + min(일차 ÷ 나눔수, 상한). 나눗셈은 내림.</summary>
        [Serializable]
        public struct DailyCount
        {
            [Tooltip("기본 개수 (개)")]
            [SerializeField, Min(0)] private int baseCount;
            [Tooltip("일차를 나누는 수 — 1이면 하루에 1개씩 (일)")]
            [SerializeField, Min(1)] private int dayDivisor;
            [Tooltip("일차로 늘어나는 양의 상한 (개)")]
            [SerializeField, Min(0)] private int dayBonusCap;

            public int BaseCount => baseCount;
            public int DayDivisor => Mathf.Max(1, dayDivisor);
            public int DayBonusCap => dayBonusCap;
        }

        [Header("개수")]
        [Tooltip("쓰레기 개수 규칙")]
        [SerializeField] private DailyCount trash;
        [Tooltip("얼룩 개수 규칙")]
        [SerializeField] private DailyCount stain;

        [Header("배치")]
        [Tooltip("쓰레기 · 얼룩 사이 최소 간격 (m)")]
        [SerializeField, Min(0f)] private float minSpacing;
        [Tooltip("한 개를 놓을 자리를 찾는 최대 시도 (회)")]
        [SerializeField, Min(1)] private int placementAttempts = 1;

        [Header("쓰레기")]
        [Tooltip("쓰레기 아이템 (Data/Items/item_trash) — 바닥에 놓이고, 주우면 인벤토리 칸을 차지하며, 창고에 넣을 수 없다")]
        [SerializeField] private ItemData trashItem;

        public DailyCount Trash => trash;
        public DailyCount Stain => stain;
        public float MinSpacing => minSpacing;
        public int PlacementAttempts => placementAttempts;
        public ItemData TrashItem => trashItem;
    }
}
