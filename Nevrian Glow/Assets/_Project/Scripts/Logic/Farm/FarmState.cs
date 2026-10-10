using System;
using System.Collections.Generic;

namespace NevrianGlowFarm.Logic.Farm
{
    /// <summary>
    /// 밭 전체 (열린 구획의 칸 목록). 칸 번호는 FarmLayout 규칙을 따른다 — 구획마다 cellsPerPlot칸이 이어진다.
    /// 작물 규칙은 id로 찾는다 (specOf). 엔진 의존 없음.
    /// </summary>
    public sealed class FarmState
    {
        private readonly List<FarmCell> cells = new List<FarmCell>();
        private readonly Func<string, CropSpec> specOf;

        public FarmState(int cellsPerPlot, int plotCount, Func<string, CropSpec> specOf)
        {
            if (cellsPerPlot < 1) throw new ArgumentOutOfRangeException(nameof(cellsPerPlot), cellsPerPlot, "1 이상");
            this.specOf = specOf ?? throw new ArgumentNullException(nameof(specOf));
            CellsPerPlot = cellsPerPlot;
            SetPlotCount(plotCount);
        }

        public int CellsPerPlot { get; }
        public int PlotCount => cells.Count / CellsPerPlot;
        public IReadOnlyList<FarmCell> Cells => cells;

        /// <summary>작물 id의 규칙. 작물이 없는 칸에는 쓰지 않는다.</summary>
        public CropSpec SpecOf(string cropId) => specOf(cropId);

        /// <summary>칸의 상태 (작물이 없으면 규칙을 찾지 않는다).</summary>
        public FarmCellStatus StatusOf(int index)
        {
            var cell = cells[index];
            return FarmRules.GetStatus(cell, cell.HasCrop ? specOf(cell.CropId) : default);
        }

        /// <summary>구획 수를 늘린다 (구획 구매 3-3). 줄이지 않는다.</summary>
        public void SetPlotCount(int plotCount)
        {
            if (plotCount < PlotCountOrZero())
                throw new ArgumentOutOfRangeException(nameof(plotCount), plotCount, "구획은 줄일 수 없음");
            while (cells.Count < plotCount * CellsPerPlot)
                cells.Add(new FarmCell());
        }

        private int PlotCountOrZero() => CellsPerPlot > 0 ? cells.Count / CellsPerPlot : 0;
    }

    /// <summary>밤 처리 단계 하나 (GDD 4-2 순서). 데모에 없는 단계(장치 · 가공)는 빈 단계로 자리만 둔다.</summary>
    public interface INightStep
    {
        string Name { get; }
        void Run(FarmState farm);
    }

    /// <summary>
    /// 밤 처리 (정산 → 다음 날 전환 시 1회, GDD 4-2):
    /// 스프링클러 물 → 비료 분사기 → 성장 → 거대 작물 판정 → 자동 수확기 → 작물 없는 칸 미개간 → (2차: 추출기 · 가공) → 상점 입고 초기화.
    /// 단계를 바꿔 끼우려면 Replace(이름, 단계) — 순서는 여기 한 곳에서만 정한다.
    /// </summary>
    public sealed class NightPipeline
    {
        public const string Sprinkler = "스프링클러 물";
        public const string FertilizerSprayer = "비료 분사기";
        public const string Growth = "성장";
        public const string GiantCrop = "거대 작물 판정";
        public const string AutoHarvester = "자동 수확기";
        public const string ResetEmptyCells = "작물 없는 칸 미개간";
        public const string Processing = "추출기 · 가공";
        public const string ShopRestock = "상점 입고 초기화";

        private readonly List<INightStep> steps;

        public NightPipeline()
        {
            steps = new List<INightStep>
            {
                new EmptyStep(Sprinkler),          // 2차 (GDD 4-9)
                new EmptyStep(FertilizerSprayer),  // 2차
                new GrowthStep(),
                new EmptyStep(GiantCrop),          // 4-4 (P1)
                new EmptyStep(AutoHarvester),      // 2차
                new ResetEmptyCellsStep(),
                new EmptyStep(Processing),         // 2차
                new EmptyStep(ShopRestock),        // 3-3
            };
        }

        public IReadOnlyList<INightStep> Steps => steps;

        /// <summary>같은 이름의 단계를 바꿔 끼운다 (예: 4-4 거대 작물, 3-3 상점 입고). 없는 이름이면 예외.</summary>
        public void Replace(string name, INightStep step)
        {
            int index = steps.FindIndex(s => s.Name == name);
            if (index < 0) throw new ArgumentException($"밤 처리에 '{name}' 단계가 없음", nameof(name));
            steps[index] = step ?? throw new ArgumentNullException(nameof(step));
        }

        public void Run(FarmState farm)
        {
            foreach (var step in steps)
                step.Run(farm);
        }

        private sealed class EmptyStep : INightStep
        {
            public EmptyStep(string name) => Name = name;
            public string Name { get; }
            public void Run(FarmState farm) { }
        }

        private sealed class GrowthStep : INightStep
        {
            public string Name => Growth;
            public void Run(FarmState farm)
            {
                foreach (var cell in farm.Cells)
                    if (cell.HasCrop)
                        FarmRules.GrowOneNight(cell, farm.SpecOf(cell.CropId));
            }
        }

        private sealed class ResetEmptyCellsStep : INightStep
        {
            public string Name => ResetEmptyCells;
            public void Run(FarmState farm)
            {
                foreach (var cell in farm.Cells)
                    FarmRules.ResetIfEmpty(cell);
            }
        }
    }
}
