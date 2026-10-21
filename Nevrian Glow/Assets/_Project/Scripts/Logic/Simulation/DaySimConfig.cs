using System.Collections.Generic;
using NevrianGlowFarm.Logic.Farm;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Sales;
using NevrianGlowFarm.Logic.Shop;

namespace NevrianGlowFarm.Logic.Simulation
{
    /// <summary>봇이 다루는 작물 한 종: 성장 규칙 · 판매가 + 그 씨앗을 파는 상품.</summary>
    public sealed class SimCrop
    {
        public string Id { get; set; }
        public CropSpec Spec { get; set; }
        public int SellPrice { get; set; }
        /// <summary>씨앗 상품 (반복 상품 — 첫 단계 가격 = 씨앗가). 상점에 없으면 Id가 null인 기본값.</summary>
        public ProductSpec SeedProduct { get; set; }
        /// <summary>한 번 살 때 받는 씨앗 수.</summary>
        public int SeedsPerBuy { get; set; } = 1;

        public bool HasSeedProduct => SeedProduct.Id != null && SeedProduct.Tiers != null && SeedProduct.Tiers.Count > 0;
        public int SeedPrice => HasSeedProduct ? SeedProduct.Tiers[0].Price : 0;
        /// <summary>칸당 일일 이익 (BALANCE 3-5) — 봇이 무엇을 심을지 고르는 기준.</summary>
        public double ProfitPerCellPerDay => CropEconomics.ProfitPerCellPerDay(Spec, SeedPrice / System.Math.Max(1, SeedsPerBuy), SellPrice);
    }

    /// <summary>봇이 배우는 스킬 (BALANCE 4번 5 · 9-3 임시 비용 — 스킬 데이터는 4-3).</summary>
    public sealed class SimSkill
    {
        public string Id { get; set; }
        /// <summary>단계별 비용 (SP) — 길이 = 최대 단계.</summary>
        public int[] Costs { get; set; } = new int[0];
        public string PrerequisiteId { get; set; }
        public int PrerequisiteLevel { get; set; }
    }

    /// <summary>GDD 6장 일차별 목표 판정 종류. 끝에만 추가한다.</summary>
    public enum SimGoalKind
    {
        /// <summary>그날 새로 심은 칸 ≥ Amount (CropId 비우면 모든 작물).</summary>
        PlantedAtLeast,
        /// <summary>그날 수확한 개수 ≥ Amount (CropId 비우면 모든 작물).</summary>
        HarvestedAtLeast,
        /// <summary>그날 상점에 그 작물 씨앗이 보인다(해금).</summary>
        CropUnlocked,
        /// <summary>그날 새로 심은 칸이 전날보다 많다.</summary>
        PlantedMoreThanPreviousDay,
    }

    /// <summary>일차별 목표 한 줄 (GDD 6장 표 → 판정 조건, 데이터로 정한다).</summary>
    public sealed class SimGoal
    {
        public int Day { get; set; }
        public SimGoalKind Kind { get; set; }
        public string CropId { get; set; }
        public int Amount { get; set; } = 1;
        /// <summary>리포트에 쓰는 설명 (예: "버섯꽃 4칸 심기").</summary>
        public string Label { get; set; }
    }

    /// <summary>측정 봇 한 판의 조건 (데이터 에셋에서 옮겨 온다 — 봇은 게임과 같은 Logic 규칙을 쓴다).</summary>
    public sealed class DaySimConfig
    {
        public int Days { get; set; } = 6;
        public int StartMoney { get; set; }
        /// <summary>시작 씨앗 (작물 id → 개수, 핫바 · 가방의 씨앗).</summary>
        public Dictionary<string, int> StartSeeds { get; set; } = new Dictionary<string, int>();
        /// <summary>시작 수확물 (작물 id → 개수, 창고 · 핫바의 작물).</summary>
        public Dictionary<string, int> StartCrops { get; set; } = new Dictionary<string, int>();
        public int CellsPerPlot { get; set; } = 16;
        public int StartPlots { get; set; } = 1;
        public List<SimCrop> Crops { get; set; } = new List<SimCrop>();
        /// <summary>밭 구획 상품 (없으면 Id null). PlotsPerTier = 한 단계에 늘어나는 구획.</summary>
        public ProductSpec PlotProduct { get; set; }
        public int PlotsPerTier { get; set; } = 1;
        /// <summary>true = 구획 살 돈이 있으면 구획 먼저(BALANCE 4번 1), false = 씨앗을 먼저 사고 남은 돈으로 구획 (4-2 실험).</summary>
        public bool PlotBeforeSeeds { get; set; } = true;
        public int ShelfSlots { get; set; } = 6;
        public int ShelfItemsPerSlot { get; set; } = 4;
        public SalesSpec Sales { get; set; }
        public LevelCurve Curve { get; set; }
        /// <summary>장사 시작 때 청결도 (BALANCE 4번 3: 0.9).</summary>
        public double Cleanliness { get; set; } = 0.9;
        /// <summary>입소문 손님 모델 값 (게임과 같은 CustomerModel, P-2).</summary>
        public CustomerModelSpec CustomerModel { get; set; }
        /// <summary>광고 계수 p (봇은 간판을 배우지 않으면 기본값).</summary>
        public double AdRate { get; set; } = 0.02;
        /// <summary>가구 자리 (손님 상한 = 자리 × 3).</summary>
        public int FurnitureSpots { get; set; } = 8;
        /// <summary>정돈도 가정 (가게 정리 P-3 전에는 1).</summary>
        public double Tidiness { get; set; } = 1.0;
        /// <summary>손님 수 ± 무작위 용도 번호 — 게임(SalesSystem)과 같은 규칙: SeedFor(시드, 6, 일차).</summary>
        public const int CountPurpose = 6;
        /// <summary>SP를 쓰는 순서 — 앞 스킬을 다 배울 때까지 뒤 스킬은 기다린다(아껴 둠).</summary>
        public List<SimSkill> SkillPriority { get; set; } = new List<SimSkill>();
        public List<SimGoal> Goals { get; set; } = new List<SimGoal>();
        /// <summary>손님 구매 난수 용도 번호 — 게임(SalesSystem)과 같은 규칙: SeedFor(시드, 2, 일차).</summary>
        public const int PurchasePurpose = 2;
    }
}
