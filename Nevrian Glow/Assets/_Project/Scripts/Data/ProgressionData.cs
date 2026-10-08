using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>경험치 · 레벨 (BALANCE 2-7). 레벨업 필요량 = 기본 + 레벨당 증가 × 현재 레벨.</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Settings/Progression", fileName = "Progression")]
    public class ProgressionData : ScriptableObject
    {
        [Header("경험치")]
        [Tooltip("매출 1달러당 경험치 (점)")]
        [SerializeField, Min(0f)] private float expPerRevenue;

        [Header("레벨업")]
        [Tooltip("레벨업 필요 경험치의 기본값 (점)")]
        [SerializeField, Min(1)] private int levelUpBase = 1;
        [Tooltip("현재 레벨 1당 늘어나는 필요 경험치 (점)")]
        [SerializeField, Min(0)] private int levelUpPerLevel;
        [Tooltip("레벨업마다 얻는 스킬 포인트 (SP)")]
        [SerializeField, Min(0)] private int skillPointsPerLevel;

        public float ExpPerRevenue => expPerRevenue;
        public int LevelUpBase => levelUpBase;
        public int LevelUpPerLevel => levelUpPerLevel;
        public int SkillPointsPerLevel => skillPointsPerLevel;
    }
}
