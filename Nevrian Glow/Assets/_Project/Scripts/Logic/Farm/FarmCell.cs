namespace NevrianGlowFarm.Logic.Farm
{
    /// <summary>작물 규칙에 필요한 값 (CropData에서 옮겨 온다, BALANCE 2-2).</summary>
    public readonly struct CropSpec
    {
        public int GrowDays { get; }
        public int WetDays { get; }
        public int HarvestYield { get; }
        /// <summary>재수확 간격. 0이면 재수확 없음.</summary>
        public int RegrowDays { get; }
        /// <summary>한 포기 총 수확 횟수.</summary>
        public int MaxHarvests { get; }

        public CropSpec(int growDays, int wetDays, int harvestYield, int regrowDays, int maxHarvests)
        {
            GrowDays = growDays < 1 ? 1 : growDays;
            WetDays = wetDays < 1 ? 1 : wetDays;
            HarvestYield = harvestYield < 1 ? 1 : harvestYield;
            RegrowDays = regrowDays < 0 ? 0 : regrowDays;
            MaxHarvests = maxHarvests < 1 ? 1 : maxHarvests;
        }
    }

    /// <summary>칸 상태 (GDD 4-2). 미개간 → 개간 중 → 개간됨 → 심음(자라는 중) → 다 자람.</summary>
    public enum FarmCellStatus
    {
        Untilled,
        Tilling,
        Tilled,
        Growing,
        Ready,
    }

    /// <summary>
    /// 밭 한 칸의 상태. 규칙은 FarmRules가 바꾸고, 여기는 값만 가진다 (저장 6-1 대상).
    /// 작물은 id로만 가진다 — 성장 규칙은 CropSpec으로 받는다.
    /// </summary>
    public sealed class FarmCell
    {
        /// <summary>개간 진행량 (작업량). 작물이 없을 때만 의미가 있다.</summary>
        public float TillProgress { get; internal set; }
        /// <summary>개간이 끝났는지.</summary>
        public bool IsTilled { get; internal set; }

        /// <summary>심은 작물 id. 없으면 null.</summary>
        public string CropId { get; internal set; }
        /// <summary>지금 목표(첫 성장 또는 재수확)를 향해 자란 날 수.</summary>
        public int Age { get; internal set; }
        /// <summary>남은 수분 일수. 0이면 오늘 밤 자라지 않는다.</summary>
        public int WetDaysLeft { get; internal set; }
        /// <summary>이 포기에서 수확한 횟수.</summary>
        public int HarvestCount { get; internal set; }
        /// <summary>비료로 줄인 필요 성장일 (4-6). 작물이 사라지면 0.</summary>
        public int FertilizerBonus { get; internal set; }

        public bool HasCrop => CropId != null;
        public bool IsWet => WetDaysLeft > 0;
    }
}
