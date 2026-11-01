using System;

namespace NevrianGlowFarm.Logic.Farm
{
    /// <summary>수확 결과. 수확하지 못했으면 Succeeded = false.</summary>
    public readonly struct HarvestResult
    {
        public bool Succeeded { get; }
        public string CropId { get; }
        public int Yield { get; }
        /// <summary>마지막 수확이라 작물이 사라지고 칸이 미개간으로 돌아갔는지.</summary>
        public bool CropRemoved { get; }

        public HarvestResult(string cropId, int yield, bool cropRemoved)
        {
            Succeeded = true;
            CropId = cropId;
            Yield = yield;
            CropRemoved = cropRemoved;
        }

        public static readonly HarvestResult None = default;
    }

    /// <summary>
    /// 밭 칸 규칙 (GDD 4-2, BALANCE 3-1 · 3-1b). 엔진 의존 없음 — 게임 · 측정 봇 · 테스트가 같은 코드를 쓴다.
    /// 모든 Try 함수는 바뀐 것이 없으면 false를 돌려주고 칸을 건드리지 않는다.
    /// </summary>
    public static class FarmRules
    {
        public static FarmCellStatus GetStatus(FarmCell cell, CropSpec spec)
        {
            if (cell.HasCrop)
                return IsReady(cell, spec) ? FarmCellStatus.Ready : FarmCellStatus.Growing;
            if (cell.IsTilled)
                return FarmCellStatus.Tilled;
            return cell.TillProgress > 0f ? FarmCellStatus.Tilling : FarmCellStatus.Untilled;
        }

        /// <summary>지금 목표까지 필요한 성장일. 처음은 성장일, 재수확 중이면 재수확 간격 (최소 1). 비료는 성장일을 줄이지 않고 자란 날을 더한다 (10/30).</summary>
        public static int RequiredDays(FarmCell cell, CropSpec spec)
        {
            int baseDays = cell.HarvestCount > 0 && spec.RegrowDays > 0 ? spec.RegrowDays : spec.GrowDays;
            return Math.Max(1, baseDays);
        }

        public static bool IsReady(FarmCell cell, CropSpec spec) => cell.HasCrop && cell.Age >= RequiredDays(cell, spec);

        /// <summary>괭이 1회. 작물이 없고 아직 개간되지 않은 칸만. 진행량이 필요 작업량에 닿으면 개간됨.</summary>
        public static bool TryTill(FarmCell cell, float workPerUse, float requiredWork)
        {
            if (cell.HasCrop || cell.IsTilled || workPerUse <= 0f)
                return false;
            cell.TillProgress += workPerUse;
            // 1.5 × 2 = 3 같은 소수 합이 3 바로 아래로 떨어지는 것을 막는다.
            if (cell.TillProgress >= requiredWork - 1e-4f)
            {
                cell.TillProgress = requiredWork;
                cell.IsTilled = true;
            }
            return true;
        }

        /// <summary>개간된 빈 칸에 심는다. 심은 날은 마른 상태.</summary>
        public static bool TryPlant(FarmCell cell, string cropId)
        {
            if (cropId == null || cell.HasCrop || !cell.IsTilled)
                return false;
            cell.CropId = cropId;
            cell.Age = 0;
            cell.WetDaysLeft = 0;
            cell.HarvestCount = 0;
            cell.FertilizerBonus = 0;
            cell.PendingFertilizer = 0;
            return true;
        }

        /// <summary>
        /// 새 게임 시작 작물 (수정사항 10/29 "창고 작물을 밭에 심어진 상태로 시작"): 빈 칸을 개간하고 다 자란 작물을 둔다. 작물이 이미 있으면 false.
        /// </summary>
        public static bool PlantReady(FarmCell cell, string cropId, CropSpec spec)
        {
            if (cropId == null || cell.HasCrop)
                return false;
            cell.IsTilled = true;
            cell.TillProgress = 0f;
            TryPlant(cell, cropId);
            cell.Age = spec.GrowDays;
            return true;
        }

        /// <summary>비료를 줄 수 있는지: 작물이 있고 아직 다 자라지 않았고 이 작물에 비료를 준 적이 없음.</summary>
        public static bool CanFertilize(FarmCell cell, CropSpec spec) => cell.HasCrop && !IsReady(cell, spec) && !cell.IsFertilized;

        /// <summary>
        /// 비료 (기획 shop.csv 10/30 "주는 즉시 1일 성장, 작물당 1회"): 준 순간 days일 자란다 (물 · 밤과 상관없이, 다 자란 날을 넘지 않음).
        /// 성장일 1일 작물(월광)은 심은 날 바로 수확할 수 있다. FertilizerBonus = 준 비료(작물당 1회 표시) — 재수확 간격은 줄이지 않는다.
        /// </summary>
        public static bool TryFertilize(FarmCell cell, CropSpec spec, int days)
        {
            if (days <= 0 || !CanFertilize(cell, spec))
                return false;
            cell.Age = Math.Min(cell.Age + days, RequiredDays(cell, spec));
            cell.FertilizerBonus = days;
            return true;
        }

        /// <summary>물이 필요한지: 물을 먹는 작물이 자라는 중이고 수분이 가득 차지 않음.</summary>
        public static bool NeedsWatering(FarmCell cell, CropSpec spec) =>
            cell.HasCrop && spec.NeedsWater && !IsReady(cell, spec) && cell.WetDaysLeft < spec.WetDays;

        /// <summary>자라는 중인 작물에 물. 수분이 작물의 wetDays로 채워진다. 물이 필요 없는 작물 · 이미 가득 · 다 자랐으면 false.</summary>
        public static bool TryWater(FarmCell cell, CropSpec spec)
        {
            if (!NeedsWatering(cell, spec))
                return false;
            cell.WetDaysLeft = spec.WetDays;
            return true;
        }

        /// <summary>
        /// 다 자란 작물을 수확한다. 재수확 작물은 마지막 수확 전까지 칸을 유지하고 다시 자란다.
        /// 작물이 사라지면 칸은 미개간으로 돌아간다. 넣을 칸 확인은 부르는 쪽이 먼저 한다.
        /// </summary>
        public static HarvestResult TryHarvest(FarmCell cell, CropSpec spec)
        {
            if (!IsReady(cell, spec))
                return HarvestResult.None;

            string cropId = cell.CropId;
            cell.HarvestCount++;
            bool regrows = spec.RegrowDays > 0 && cell.HarvestCount < spec.MaxHarvests;
            if (regrows)
            {
                cell.Age = 0;
                return new HarvestResult(cropId, spec.HarvestYield, false);
            }

            ClearToUntilled(cell);
            return new HarvestResult(cropId, spec.HarvestYield, true);
        }

        /// <summary>
        /// 밤 성장 1회: 젖어 있는(물이 필요 없는 작물은 언제나) 자라는 중인 작물만 하루 자란다. 수분은 하루 줄어든다.
        /// (10/30 전 저장 파일의 '오늘 준 비료'는 준 표시로만 바꾼다 — 비료는 이제 준 순간 자란다.)
        /// </summary>
        public static void GrowOneNight(FarmCell cell, CropSpec spec)
        {
            if (!cell.HasCrop)
                return;
            if (cell.PendingFertilizer > 0)
            {
                cell.FertilizerBonus = cell.PendingFertilizer;
                cell.PendingFertilizer = 0;
            }
            if ((cell.WetDaysLeft > 0 || !spec.NeedsWater) && !IsReady(cell, spec))
                cell.Age++;
            if (cell.WetDaysLeft > 0)
                cell.WetDaysLeft--;
        }

        /// <summary>밤 처리: 작물이 없는 칸(개간됨 · 개간 중)은 미개간으로. 작물이 있으면 그대로.</summary>
        public static bool ResetIfEmpty(FarmCell cell)
        {
            if (cell.HasCrop || (!cell.IsTilled && cell.TillProgress <= 0f))
                return false;
            ClearToUntilled(cell);
            return true;
        }

        private static void ClearToUntilled(FarmCell cell)
        {
            cell.CropId = null;
            cell.Age = 0;
            cell.WetDaysLeft = 0;
            cell.HarvestCount = 0;
            cell.FertilizerBonus = 0;
            cell.PendingFertilizer = 0;
            cell.IsTilled = false;
            cell.TillProgress = 0f;
        }
    }
}
