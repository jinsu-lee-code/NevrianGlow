using System;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>청소 (BALANCE 2-5). 대걸레 스킬 효과는 스킬(4-3)에서 덮어쓴다.</summary>
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

        [Header("도구")]
        [Tooltip("대걸레 닦는 반경 (m)")]
        [SerializeField, Min(0.1f)] private float mopRadius = 0.1f;
        [Tooltip("대걸레 닦는 속도 (얼룩 진하기/초)")]
        [SerializeField, Min(0.01f)] private float mopSpeed = 0.01f;
        [Tooltip("한 번에 들 수 있는 쓰레기 (개)")]
        [SerializeField, Min(1)] private int carryCount = 1;

        public DailyCount Trash => trash;
        public DailyCount Stain => stain;
        public float MinSpacing => minSpacing;
        public int PlacementAttempts => placementAttempts;
        public float MopRadius => mopRadius;
        public float MopSpeed => mopSpeed;
        public int CarryCount => carryCount;
    }
}
