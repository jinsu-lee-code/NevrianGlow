using System;
using System.Collections.Generic;
using NevrianGlowFarm.Logic.Simulation;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 측정 봇 조건 (TASKS 4-1, BALANCE 4번). 게임 수치는 참조한 설정 · 콘텐츠 에셋을 그대로 쓰고,
    /// 여기에는 봇만의 가정(시드 수 · 청결도 · 스킬 순서 · GDD 6장 목표 판정)만 둔다.
    /// 실행: Glow Farm/측정/측정 봇 실행 → 저장소 Reports/ 에 리포트.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Settings/Day Simulator", fileName = "DaySimulatorSettings")]
    public class DaySimulatorSettingsData : ScriptableObject
    {
        [Serializable]
        public struct SkillEntry
        {
            [Tooltip("배울 스킬 — 비용 · 선행은 스킬 데이터에서 읽는다 (P-6, 2026-10-21)")]
            [SerializeField] private SkillData skill;

            public SkillEntry(SkillData skill) => this.skill = skill;

            public SkillData Skill => skill;

            /// <summary>스킬 데이터 → 봇 스킬 (비용 · 선행 그대로). 스킬이 비면 null.</summary>
            public SimSkill ToLogic() => skill == null ? null : new SimSkill
            {
                Id = skill.Id,
                Costs = new List<int>(skill.Costs).ToArray(),
                PrerequisiteId = skill.Prerequisite != null ? skill.Prerequisite.Id : null,
                PrerequisiteLevel = skill.Prerequisite != null ? skill.PrerequisiteLevel : 0,
            };
        }

        [Serializable]
        public struct GoalEntry
        {
            [Tooltip("일차")]
            [SerializeField, Min(1)] private int day;
            [Tooltip("판정 종류")]
            [SerializeField] private SimGoalKind kind;
            [Tooltip("대상 작물 — 비우면 모든 작물")]
            [SerializeField] private CropData crop;
            [Tooltip("기준 개수 (칸 · 개)")]
            [SerializeField, Min(0)] private int amount;
            [Tooltip("리포트에 쓰는 설명 (GDD 6장 표의 말)")]
            [SerializeField] private string label;

            public GoalEntry(int day, SimGoalKind kind, CropData crop, int amount, string label)
            {
                this.day = day;
                this.kind = kind;
                this.crop = crop;
                this.amount = amount;
                this.label = label;
            }

            public SimGoal ToLogic() => new SimGoal
            {
                Day = day,
                Kind = kind,
                CropId = crop != null ? crop.Id : null,
                Amount = amount,
                Label = label,
            };
        }

        [Header("참조")]
        [Tooltip("시작 자금 · 소지품 · 밭 (Data/Stats/GameStartSettings)")]
        [SerializeField] private GameStartSettingsData gameStart;
        [Tooltip("손님 수 · 구매 개수 (Data/Stats/SalesSettings)")]
        [SerializeField] private SalesSettingsData sales;
        [Tooltip("경험치 · 레벨 (Data/Stats/Progression)")]
        [SerializeField] private ProgressionData progression;
        [Tooltip("진열대 칸 수 · 칸당 개수 (Data/Stats/InventorySettings)")]
        [SerializeField] private InventorySettingsData inventory;
        [Tooltip("작물 · 상품 (Data/ContentDatabase)")]
        [SerializeField] private ContentDatabase database;
        [Tooltip("쓰레기 · 얼룩 · 흩어진 상품 · 흐트러진 칸 개수 — 정리 클릭 수 (Data/Stats/CleaningSettings)")]
        [SerializeField] private CleaningSettingsData cleaning;

        [Header("조정값")]
        [Tooltip("돌릴 시드 수 (판)")]
        [SerializeField, Min(1)] private int seedCount = 20;
        [Tooltip("첫 시드 — 시드는 이 값부터 1씩 (게임 SalesSystem 시드와 같은 규칙)")]
        [SerializeField] private int baseSeed = 20261011;
        [Tooltip("돌릴 일차 수 (일)")]
        [SerializeField, Min(1)] private int days = 6;
        [Tooltip("장사 시작 때 청결도 — BALANCE 4번 3: 혼자 다 못 치운 10% 남김 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float cleanliness = 0.9f;
        [Tooltip("정돈도 가정 — 만족도에 들어감. 가게 정리(P-3) 전에는 1 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float tidiness = 1f;
        [Tooltip("켜면 구획 살 돈이 있으면 구획 먼저(BALANCE 4번 1), 끄면 씨앗을 먼저 사고 남은 돈으로 구획")]
        [SerializeField] private bool plotBeforeSeeds = true;
        [Tooltip("1일차에 심는 칸 상한 — 튜토리얼대로 따라가는 플레이어 실험 (칸, 0 = 제한 없음)")]
        [SerializeField, Min(0)] private int firstDayCells;
        [Tooltip("SP를 쓰는 순서 (BALANCE 4번 5: 작물 해금 순). 앞 스킬을 다 배울 때까지 뒤는 아껴 둔다. 숨김 스킬은 봇도 배우지 않음")]
        [SerializeField] private List<SkillEntry> skillPriority = new List<SkillEntry>();
        [Tooltip("GDD 1페이지 데모 목표 경험 — 일차별 판정 (봇 규칙 8)")]
        [SerializeField] private List<GoalEntry> goals = new List<GoalEntry>();

        public GameStartSettingsData GameStart => gameStart;
        public SalesSettingsData Sales => sales;
        public ProgressionData Progression => progression;
        public InventorySettingsData Inventory => inventory;
        public ContentDatabase Database => database;
        public CleaningSettingsData Cleaning => cleaning;
        public int SeedCount => seedCount;
        public int BaseSeed => baseSeed;
        public int Days => days;
        public float Cleanliness => cleanliness;
        public float Tidiness => tidiness;
        public bool PlotBeforeSeeds => plotBeforeSeeds;
        public int FirstDayCells => firstDayCells;
        public IReadOnlyList<SkillEntry> SkillPriority => skillPriority;
        public IReadOnlyList<GoalEntry> Goals => goals;
    }
}
