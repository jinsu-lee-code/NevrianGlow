namespace NevrianGlowFarm.Logic.Farm
{
    /// <summary>
    /// 작물 한 포기의 수지 (BALANCE 3-5 칸당 일일 이익). 인스펙터 · 측정 봇(4-1)이 같은 계산을 쓴다.
    /// 판매가 배율(고품질 스킬) · 비료 · 거대 작물은 넣지 않은 기본값.
    /// </summary>
    public static class CropEconomics
    {
        /// <summary>심고 마지막 수확까지 걸리는 날 수: 성장일 + 재수확 간격 × (수확 횟수 − 1).</summary>
        public static int TotalDays(CropSpec spec)
        {
            int harvests = HarvestCount(spec);
            return spec.GrowDays + spec.RegrowDays * (harvests - 1);
        }

        /// <summary>한 포기에서 수확하는 횟수 (재수확 없으면 1).</summary>
        public static int HarvestCount(CropSpec spec) => spec.RegrowDays > 0 ? spec.MaxHarvests : 1;

        /// <summary>한 포기 순이익 = 판매가 × 수확량 × 수확 횟수 − 씨앗가.</summary>
        public static int ProfitPerPlant(CropSpec spec, int seedPrice, int sellPrice) =>
            sellPrice * spec.HarvestYield * HarvestCount(spec) - seedPrice;

        /// <summary>칸당 일일 이익 = 한 포기 순이익 ÷ 전체 날 수.</summary>
        public static double ProfitPerCellPerDay(CropSpec spec, int seedPrice, int sellPrice) =>
            (double)ProfitPerPlant(spec, seedPrice, sellPrice) / TotalDays(spec);
    }
}
