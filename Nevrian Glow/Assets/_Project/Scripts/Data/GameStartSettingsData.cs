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
        public struct StartingItem
        {
            [Tooltip("아이템 — 비우면 빈 칸")]
            [SerializeField] private ItemData item;
            [Tooltip("개수 — 칸당 최대를 넘으면 최대까지만 (개)")]
            [SerializeField, Min(1)] private int count;

            public ItemData Item => item;
            public int Count => count;
        }

        [Header("시작 상태")]
        [Tooltip("시작 자금 (달러)")]
        [SerializeField, Min(0)] private int startingMoney;
        [Tooltip("시작 핫바 — 1번 칸부터 순서대로, 아이템을 비운 줄은 빈 칸 (BALANCE 2-3)")]
        [SerializeField] private List<StartingItem> startingHotbarItems = new List<StartingItem>();
        [Tooltip("시작 창고 — 바로 팔 수 있는 시작 작물, 가게에서 쓸 대걸레 등 (BALANCE 2-3)")]
        [SerializeField] private List<StartingItem> startingStorageItems = new List<StartingItem>();

        [Header("밭 구획")]
        [Tooltip("처음 열려 있는 구획 수 (개)")]
        [SerializeField, Min(1)] private int startPlotCount = 1;
        [Tooltip("최대 구획 수 (개)")]
        [SerializeField, Min(1)] private int maxPlotCount = 1;
        [Tooltip("구획 하나의 칸 수 (칸)")]
        [SerializeField, Min(1)] private int cellsPerPlot = 1;

        [Header("개간")]
        [Tooltip("칸 하나를 개간하는 데 필요한 작업량 — 괭이 1회 작업량으로 나눈 만큼 갈아야 한다 (작업량)")]
        [SerializeField, Min(0.01f)] private float tillWorkRequired = 0.01f;

        [Header("밭 간격")]
        [Tooltip("칸 사이 간격 (m)")]
        [SerializeField, Min(0.1f)] private float cellPitch = 0.1f;
        [Tooltip("구획 안 밭(2×2) 사이 간격 (m)")]
        [SerializeField, Min(0.1f)] private float bedPitch = 0.1f;
        [Tooltip("구획 사이 간격 (m)")]
        [SerializeField, Min(0.1f)] private float plotPitch = 0.1f;
        [Tooltip("구획이 늘어나는 방향 (단위 벡터)")]
        [SerializeField] private Vector3 plotExpandDirection = Vector3.left;

        [Header("데모")]
        [Tooltip("데모 마지막 일차 — 이날 정산 뒤 데모 종료 화면(계속 플레이 가능). 0이면 종료 화면 없음 (일차)")]
        [SerializeField, Min(0)] private int demoLastDay;

        public int StartingMoney => startingMoney;
        public IReadOnlyList<StartingItem> StartingHotbar => startingHotbarItems;
        public IReadOnlyList<StartingItem> StartingStorage => startingStorageItems;
        public int StartPlotCount => startPlotCount;
        public int MaxPlotCount => maxPlotCount;
        public int CellsPerPlot => cellsPerPlot;
        public float TillWorkRequired => tillWorkRequired;
        public float CellPitch => cellPitch;
        public float BedPitch => bedPitch;
        public float PlotPitch => plotPitch;
        public Vector3 PlotExpandDirection => plotExpandDirection;
        public int DemoLastDay => demoLastDay;
    }
}
