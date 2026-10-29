using System.Collections.Generic;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 작물 한 종 (BALANCE 2-2). 수확물도 이 에셋 그대로 인벤토리 칸에 들어간다(ItemData).
    /// 단계 프리팹 · 커스텀 인스펙터는 2-1에서 추가한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Data/Crop", fileName = "crop_new")]
    public class CropData : ItemData
    {
        [Header("모습")]
        [Tooltip("그레이박스 작물 색 — 모델 교체(5-2) 전까지 (BALANCE 9-5: 월광 시안 · 버섯꽃 보라 · 등불 금빛)")]
        [SerializeField] private Color greyboxColor = Color.white;
        [Tooltip("성장 단계 프리팹 (새싹 → 다 자람 순) — 비어 있으면 그레이박스 구로 표시. 모델 교체(5-2) 때 채운다")]
        [SerializeField] private List<GameObject> stagePrefabs = new List<GameObject>();
        [Tooltip("단계 모델 크기 배율 — 밭 칸 배율과 곱해진다. 낮은 작물을 키워 시선이 땅에 머물지 않게 (배, 2026-10-25)")]
        [SerializeField, Range(0.5f, 3f)] private float modelScale = 1f;
        [Tooltip("켜면 밭 칸이 아니라 작물 모델(나무 · 열매)을 조준해야 수확 — 기획 growth_form = tree (수정사항 댓글 10/29)")]
        [SerializeField] private bool harvestFromModel;

        [Header("가격")]
        [Tooltip("씨앗 1개 가격 (달러)")]
        [SerializeField, Min(0)] private int seedPrice;
        [Tooltip("수확물 1개 판매가 (달러)")]
        [SerializeField, Min(0)] private int sellPrice;

        [Header("성장")]
        [Tooltip("심은 뒤 수확까지 자라야 하는 날 수 (일)")]
        [SerializeField, Min(1)] private int growDays = 1;
        [Tooltip("물을 줘야 자라는지 — 끄면 물 없이 밤마다 자란다 (버섯꽃, 기획 crops.csv needs_water)")]
        [SerializeField] private bool needsWater = true;
        [Tooltip("물을 한 번 주면 젖은 상태가 유지되는 날 수 (일)")]
        [SerializeField, Min(1)] private int wetDays = 1;
        [Tooltip("한 번 수확할 때 얻는 개수 (개)")]
        [SerializeField, Min(1)] private int harvestYield = 1;
        [Tooltip("수확 후 다시 열매가 맺히기까지 날 수 — 0이면 재수확 없음 (일)")]
        [SerializeField, Min(0)] private int regrowDays;
        [Tooltip("한 포기에서 수확할 수 있는 총 횟수 — 1이면 한 번 수확하고 끝, 다 쓰면 고사 (회)")]
        [SerializeField, Min(1)] private int maxHarvests = 1;

        [Header("거대 작물")]
        [Tooltip("수확 가능해질 때 거대 작물이 될 확률 — 0이면 거대화 안 됨 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float giantChance;
        [Tooltip("거대 작물 수확량 배율 (배)")]
        [SerializeField, Min(1f)] private float giantYieldMultiplier = 1f;
        [Tooltip("거대 작물 크기 배율 (배)")]
        [SerializeField, Min(1f)] private float giantScale = 1f;

        public int SeedPrice => seedPrice;
        public int SellPrice => sellPrice;
        public int GrowDays => growDays;
        public bool NeedsWater => needsWater;
        public int WetDays => wetDays;
        public int HarvestYield => harvestYield;
        public int RegrowDays => regrowDays;
        public int MaxHarvests => maxHarvests;
        public bool CanRegrow => regrowDays > 0 && maxHarvests > 1;
        public float GiantChance => giantChance;
        public float GiantYieldMultiplier => giantYieldMultiplier;
        public float GiantScale => giantScale;
        public Color GreyboxColor => greyboxColor;
        public IReadOnlyList<GameObject> StagePrefabs => stagePrefabs;
        public float ModelScale => modelScale;
        public bool HarvestFromModel => harvestFromModel;

        /// <summary>규칙 계산용 값 묶음 (Logic FarmRules · CropEconomics).</summary>
        public Logic.Farm.CropSpec ToSpec() => new Logic.Farm.CropSpec(growDays, wetDays, harvestYield, regrowDays, maxHarvests, needsWater);
    }
}
