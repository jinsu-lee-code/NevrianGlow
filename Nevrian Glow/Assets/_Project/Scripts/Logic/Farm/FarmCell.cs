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
        /// <summary>물을 줘야 자라는지. false면 물 없이 밤마다 자란다 (버섯꽃 — 기획 crops.csv needs_water, 회신 #30).</summary>
        public bool NeedsWater { get; }

        public CropSpec(int growDays, int wetDays, int harvestYield, int regrowDays, int maxHarvests, bool needsWater = true)
        {
            GrowDays = growDays < 1 ? 1 : growDays;
            WetDays = wetDays < 1 ? 1 : wetDays;
            HarvestYield = harvestYield < 1 ? 1 : harvestYield;
            RegrowDays = regrowDays < 0 ? 0 : regrowDays;
            MaxHarvests = maxHarvests < 1 ? 1 : maxHarvests;
            NeedsWater = needsWater;
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
        /// <summary>준 비료로 자란 날 수 — 작물당 1회 표시 (10/30부터 준 순간 자람, 그 전은 줄인 성장일). 작물이 사라지면 0.</summary>
        public int FertilizerBonus { get; internal set; }
        /// <summary>10/30 전 저장 파일의 '오늘 준 비료' — 밤에 FertilizerBonus(준 표시)가 된다. 새로 생기지 않는다. 작물이 사라지면 0.</summary>
        public int PendingFertilizer { get; internal set; }

        /// <summary>이 작물에 비료를 이미 줬는지 (작물당 1회).</summary>
        public bool IsFertilized => FertilizerBonus > 0 || PendingFertilizer > 0;

        public bool HasCrop => CropId != null;
        public bool IsWet => WetDaysLeft > 0;
    }
}
