namespace NevrianGlowFarm.Logic.Farm
{
    /// <summary>
    /// 칸 번호 → 밭 원점 기준 위치 (BALANCE 2-3 · 9-1, 노션 GDD 12.3).
    /// 구획마다 cellsPerPlot칸, 칸 4개 = 밭(2×2) 하나, 밭은 두 줄(across 0 / 1)로 놓이고 구획이 늘수록 along 방향으로 이어진다.
    /// 번호 순서: 칸 i의 같은 밭 짝 = i^1, 같은 밭 = (i &amp; ~3)+k, 같은 줄 이웃 밭 = (i &amp; ~7)+k (물뿌리개 2-7).
    /// along = 확장 방향(BALANCE: -X), across = 줄 방향(+Z). 원점 = 첫 구획 첫 밭의 중심.
    /// </summary>
    public static class FarmLayout
    {
        public const int CellsPerBed = 4;
        public const int BedRows = 2;

        /// <summary>칸 중심의 (along, across) 거리 (m).</summary>
        public static (double Along, double Across) CellOffset(int index, int cellsPerPlot, double cellPitch, double bedPitch, double plotPitch)
        {
            int plot = index / cellsPerPlot;
            int inPlot = index % cellsPerPlot;
            int bed = inPlot / CellsPerBed;
            int inBed = inPlot % CellsPerBed;
            int bedsPerRow = BedsPerRow(cellsPerPlot);

            int bedColumn = bed % bedsPerRow;
            int bedRow = bed / bedsPerRow;
            int cellColumn = inBed % 2;
            int cellRow = inBed / 2;

            double along = plot * plotPitch + bedColumn * bedPitch + (cellColumn - 0.5) * cellPitch;
            double across = bedRow * bedPitch + (cellRow - 0.5) * cellPitch;
            return (along, across);
        }

        /// <summary>구획 하나가 차지하는 칸 중심 범위 (along 최소 · 최대, across 최소 · 최대).</summary>
        public static (double AlongMin, double AlongMax, double AcrossMin, double AcrossMax) PlotCenterBounds(int plot, int cellsPerPlot, double cellPitch, double bedPitch, double plotPitch)
        {
            var first = CellOffset(plot * cellsPerPlot, cellsPerPlot, cellPitch, bedPitch, plotPitch);
            var last = CellOffset(plot * cellsPerPlot + cellsPerPlot - 1, cellsPerPlot, cellPitch, bedPitch, plotPitch);
            return (first.Along, last.Along, first.Across, last.Across);
        }

        private static int BedsPerRow(int cellsPerPlot)
        {
            int beds = cellsPerPlot / CellsPerBed;
            int perRow = beds / BedRows;
            return perRow < 1 ? 1 : perRow;
        }
    }
}
