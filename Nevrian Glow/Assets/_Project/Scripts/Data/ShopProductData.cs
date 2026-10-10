using System;
using System.Collections.Generic;
using NevrianGlowFarm.Logic.Shop;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>해금 조건 한 줄 (인스펙터 · CSV용). Logic UnlockRequirement로 바꿔 쓴다.</summary>
    [Serializable]
    public struct UnlockRequirementEntry
    {
        [Tooltip("조건 종류 — Always 없음 · SkillLevel 스킬 단계 · DayAtLeast 일차 · FirstHarvest 작물 첫 수확")]
        [SerializeField] private UnlockKind kind;
        [Tooltip("대상 id — 스킬 id(skill_*) 또는 작물 id(crop_*)")]
        [SerializeField] private string targetId;
        [Tooltip("값 — 스킬 단계 또는 일차")]
        [SerializeField, Min(0)] private int value;

        public UnlockRequirementEntry(UnlockKind kind, string targetId, int value)
        {
            this.kind = kind;
            this.targetId = targetId;
            this.value = value;
        }

        public UnlockKind Kind => kind;
        public string TargetId => targetId;
        public int Value => value;
        public UnlockRequirement ToLogic() => new UnlockRequirement(kind, targetId, value);
    }

    /// <summary>상품 단계 하나: 가격 + 해금 조건(모두 만족해야 열림).</summary>
    [Serializable]
    public struct ShopTierEntry
    {
        [Tooltip("가격 (달러)")]
        [SerializeField, Min(0)] private int price;
        [Tooltip("이 단계가 열리는 조건 — 비우면 바로 열림")]
        [SerializeField] private List<UnlockRequirementEntry> requirements;

        public ShopTierEntry(int price, List<UnlockRequirementEntry> requirements)
        {
            this.price = price;
            this.requirements = requirements;
        }

        public int Price => price;
        public IReadOnlyList<UnlockRequirementEntry> Requirements => requirements ?? (IReadOnlyList<UnlockRequirementEntry>)Array.Empty<UnlockRequirementEntry>();
    }

    /// <summary>
    /// 상점 상품 공통 (BALANCE 2-8 · 9-2, 노션 GDD 12.4). 상품 하나 = 에셋 하나 (ContentDatabase가 Data/Shop에서 자동 수집).
    /// 무엇을 주는지는 하위 종류(아이템 · 도구 등급 · 밭 구획 · 가방 · 진열대)가 정하고, 상점은 종류(타입)로 효과를 고른다 — id로 분기하지 않는다.
    /// </summary>
    public abstract class ShopProductData : ContentData
    {
        [Header("목록")]
        [Tooltip("상점 목록 순서 — 작은 값이 위")]
        [SerializeField] private int sortOrder;
        [Tooltip("분류 이름 (상점 목록에 붙는 글자, 예: 씨앗 · 도구 · 확장)")]
        [SerializeField] private string category;

        [Header("단계 · 가격")]
        [Tooltip("켜면 단계 1개를 하루 입고량만큼 여러 번 산다(씨앗 · 소모품). 끄면 단계를 차례로 한 번씩(도구 등급 · 확장)")]
        [SerializeField] private bool repeatable;
        [Tooltip("단계별 가격 · 해금 조건 — 반복 상품은 첫 줄만 쓴다")]
        [SerializeField] private List<ShopTierEntry> tiers = new List<ShopTierEntry>();

        [Header("입고")]
        [Tooltip("일차별 하루 입고량 (1일차부터, 마지막 값이 이후 계속). 비우면 무제한 (개)")]
        [SerializeField] private List<int> dailyStock = new List<int>();
        [Tooltip("처음 열린 날은 입고 없이 다음 날부터 (BALANCE 3-8 추천값 — 미채택이면 끔)")]
        [SerializeField] private bool stockFromNextDay;

        public int SortOrder => sortOrder;
        public string Category => category;
        public bool Repeatable => repeatable;
        public IReadOnlyList<ShopTierEntry> Tiers => tiers;
        public IReadOnlyList<int> DailyStock => dailyStock;
        public bool StockFromNextDay => stockFromNextDay;

        /// <summary>단계 이름 (목록 글자) — 하위 종류가 "철 괭이"처럼 바꾼다.</summary>
        public virtual string TierLabel(int tier) => repeatable || tiers.Count <= 1 ? DisplayName : $"{DisplayName} {tier + 1}단계";

        public ProductSpec ToSpec()
        {
            var specs = new List<TierSpec>(tiers.Count);
            foreach (var t in tiers)
            {
                var reqs = new List<UnlockRequirement>();
                foreach (var r in t.Requirements)
                    reqs.Add(r.ToLogic());
                specs.Add(new TierSpec(t.Price, reqs));
            }
            return new ProductSpec(Id, specs, repeatable, dailyStock, stockFromNextDay);
        }
    }
}
