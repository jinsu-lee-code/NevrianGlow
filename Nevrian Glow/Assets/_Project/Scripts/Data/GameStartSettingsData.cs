using System;
using System.Collections.Generic;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>새 게임 시작 상태와 밭 배치 규칙 (BALANCE 2-3). 구획 가격 · 씨앗 입고량은 상점 상품(3-3)에 둔다.</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Settings/Game Start", fileName = "GameStartSettings")]
    public class GameStartSettingsData : ScriptableObject
    {
        [Serializable]
        public struct StartingCrop
        {
            [Tooltip("시작할 때 들고 있는 작물")]
            [SerializeField] private CropData crop;
            [Tooltip("개수 (개)")]
            [SerializeField, Min(1)] private int count;

            public CropData Crop => crop;
            public int Count => count;
        }

        [Header("시작 상태")]
        [Tooltip("시작 자금 (달러)")]
        [SerializeField, Min(0)] private int startingMoney;
        [Tooltip("시작할 때 바로 팔 수 있는 작물")]
        [SerializeField] private List<StartingCrop> startingCrops = new List<StartingCrop>();

        [Header("밭 구획")]
        [Tooltip("처음 열려 있는 구획 수 (개)")]
        [SerializeField, Min(1)] private int startPlotCount = 1;
        [Tooltip("최대 구획 수 (개)")]
        [SerializeField, Min(1)] private int maxPlotCount = 1;
        [Tooltip("구획 하나의 칸 수 (칸)")]
        [SerializeField, Min(1)] private int cellsPerPlot = 1;

        [Header("밭 간격")]
        [Tooltip("칸 사이 간격 (m)")]
        [SerializeField, Min(0.1f)] private float cellPitch = 0.1f;
        [Tooltip("구획 안 밭(2×2) 사이 간격 (m)")]
        [SerializeField, Min(0.1f)] private float bedPitch = 0.1f;
        [Tooltip("구획 사이 간격 (m)")]
        [SerializeField, Min(0.1f)] private float plotPitch = 0.1f;
        [Tooltip("구획이 늘어나는 방향 (단위 벡터)")]
        [SerializeField] private Vector3 plotExpandDirection = Vector3.left;

        public int StartingMoney => startingMoney;
        public IReadOnlyList<StartingCrop> StartingCrops => startingCrops;
        public int StartPlotCount => startPlotCount;
        public int MaxPlotCount => maxPlotCount;
        public int CellsPerPlot => cellsPerPlot;
        public float CellPitch => cellPitch;
        public float BedPitch => bedPitch;
        public float PlotPitch => plotPitch;
        public Vector3 PlotExpandDirection => plotExpandDirection;
    }
}
