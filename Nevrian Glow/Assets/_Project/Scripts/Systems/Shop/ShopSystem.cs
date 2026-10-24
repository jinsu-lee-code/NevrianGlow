using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Game;
using NevrianGlowFarm.Logic.Shop;
using NevrianGlowFarm.Systems.Farm;
using NevrianGlowFarm.Systems.Player;
using NevrianGlowFarm.Systems.Storage;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Shop
{
    /// <summary>
    /// 상점 (노션 GDD 12.4 ShopSystem, GDD 4-6 · BALANCE 9-2). Core 씬에 하나 — 어디서나 B로 연다(상점 화면).
    /// 상품 = Data/Shop의 ShopProductData 에셋(ContentDatabase 자동 수집). 살 수 있는지 · 입고량 · 단계는 Logic ShopState,
    /// 해금 조건은 GameManager(스킬 단계 · 일차 · 첫 수확). 효과는 상품 종류(타입)로 고른다 — 아이템 지급 · 도구 교체 · 밭 구획 · 가방 · 진열대.
    /// 날이 바뀌면 입고 초기화 (밤 처리 마지막 단계, GDD 4-2).
    /// </summary>
    public class ShopSystem : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("상품 목록 (Data/ContentDatabase)")]
        [SerializeField] private ContentDatabase database;
        [Tooltip("아이템을 받을 인벤토리 (Core Player)")]
        [SerializeField] private PlayerInventory inventory;
        [Tooltip("도구 교체 때 같이 찾는 창고 (Core Storage)")]
        [SerializeField] private StorageSystem storage;

        [Header("조정값")]
        [Tooltip("살 수 있는 단계 — 그 밖의 단계에서는 상점을 보기만 (회신2 4절 #2: 상점 · 농사 단계에서만 구매)")]
        [SerializeField] private List<DayPhase> buyPhases = new List<DayPhase> { DayPhase.Shop, DayPhase.Farming };
        [Tooltip("보기만 할 때 안내 (게임 화면 글자 — '—' 쓰지 않음)")]
        [SerializeField] private string viewOnlyMessage = "농사 단계에서 살 수 있어요";
        [Tooltip("튜토리얼이 다른 상품을 잠갔을 때 안내 (5-7 buy_other)")]
        [SerializeField] private string tutorialLockMessage = "지금은 안내한 것만 살 수 있어요";

        [Header("디버그")]
        [Tooltip("상품 상태 (읽기 전용)")]
        [SerializeField] private List<string> offersView = new List<string>();
        [Tooltip("오늘 지출 (읽기 전용)")]
        [SerializeField] private int spentToday;

        private static ShopSystem instance;
        private readonly ShopState state = new ShopState();
        private readonly List<ShopProductData> products = new List<ShopProductData>();
        private FarmGrid farm;
        private readonly List<ShelfDisplay> shelves = new List<ShelfDisplay>();

        public static ShopSystem Instance => instance;
        public ShopState State => state;
        public IReadOnlyList<ShopProductData> Products => products;
        /// <summary>오늘 상점에서 쓴 달러 (정산 화면 '지출').</summary>
        public int SpentToday => state.SpentToday;

        /// <summary>상품 상태가 바뀔 수 있을 때 (구매 · 달러 · 해금 · 새 날) — 상점 화면이 다시 그린다.</summary>
        public event Action Changed;

        private void Awake()
        {
            if (database == null || inventory == null || storage == null)
            {
                Debug.LogError("[ShopSystem] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            instance = this;
            // 숨긴 상품(기획 phase2 · 연결 전)은 상점에 올리지 않는다 (회신2 2-6)
            products.AddRange(database.All<ShopProductData>().Where(p => p != null && !p.Hidden));
            products.Sort((a, b) => a.SortOrder != b.SortOrder ? a.SortOrder.CompareTo(b.SortOrder) : string.CompareOrdinal(a.Id, b.Id));
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void OnEnable()
        {
            GameEvents.DayChanged += OnDayChanged;
            GameEvents.MoneyChanged += OnMoneyChanged;
            GameEvents.SkillLevelChanged += OnSkillChanged;
            GameEvents.CropFirstHarvested += OnFirstHarvest;
        }

        private void OnDisable()
        {
            GameEvents.DayChanged -= OnDayChanged;
            GameEvents.MoneyChanged -= OnMoneyChanged;
            GameEvents.SkillLevelChanged -= OnSkillChanged;
            GameEvents.CropFirstHarvested -= OnFirstHarvest;
        }

        // 밭 · 진열대는 다른 씬(Farm · Shop)에 있어 씬 사이 참조를 걸 수 없다 → 처음 쓸 때 한 번 찾는다.
        private FarmGrid Farm => farm != null ? farm : farm = FindFirstObjectByType<FarmGrid>();

        private IReadOnlyList<ShelfDisplay> Shelves
        {
            get
            {
                if (shelves.Count == 0)
                    shelves.AddRange(FindObjectsByType<ShelfDisplay>(FindObjectsSortMode.InstanceID));
                return shelves;
            }
        }

        private void OnDayChanged(int day)
        {
            state.StartDay();
            NoteOpenAll();
            NotifyChanged();
        }

        private void OnMoneyChanged(int money) => NotifyChanged();
        private void OnSkillChanged(string skillId, int level) { NoteOpenAll(); NotifyChanged(); }
        private void OnFirstHarvest(string cropId) { NoteOpenAll(); NotifyChanged(); }

        private void NoteOpenAll()
        {
            var game = GameManager.Instance;
            if (game == null || !game.IsStarted)
                return;
            foreach (var p in products)
                state.NoteOpen(p.ToSpec(), game);
        }

        // ── 저장 (6-1) ──

        public void CaptureTo(Logic.Save.SaveSnapshot into) => Logic.Save.SaveState.CaptureShop(state, into);

        /// <summary>구매 단계 · 오늘 산 개수 · 처음 열린 날 · 오늘 지출을 바꾼다. 효과(칸 수 등)는 각 시스템의 저장 값으로 따로 돌아온다.</summary>
        public void LoadFrom(Logic.Save.SaveSnapshot from)
        {
            Logic.Save.SaveState.ApplyShop(state, from);
            NotifyChanged();
        }

        // ── 조회 ─────────────────────────────────────────

        public ProductOffer OfferOf(ShopProductData product)
        {
            var game = GameManager.Instance;
            if (product == null || game == null || !game.IsStarted)
                return default;
            return state.Offer(product.ToSpec(), game, game.Money);
        }

        /// <summary>잠긴 단계의 조건 글자 (예: "skill_watering_license 2단계 필요").</summary>
        public string LockReason(ShopProductData product, int tier)
        {
            if (product == null || tier < 0 || tier >= product.Tiers.Count)
                return string.Empty;
            var game = GameManager.Instance;
            var text = new StringBuilder();
            foreach (var r in product.Tiers[tier].Requirements)
            {
                if (game != null && UnlockRules.IsMet(r.ToLogic(), game))
                    continue;
                if (text.Length > 0)
                    text.Append(", ");
                switch (r.Kind)
                {
                    case UnlockKind.SkillLevel:
                        // 스킬 이름으로 (상점에 잠긴 상품이 보이므로 — 기획 회신 10/22 A6). 한 단계짜리 스킬은 단계를 적지 않는다
                        var skill = database.Get<SkillData>(r.TargetId);
                        string skillName = skill != null ? skill.DisplayName : r.TargetId;
                        text.Append(skill != null && skill.MaxLevel <= 1 ? $"스킬 '{skillName}'" : $"스킬 '{skillName}' {Mathf.Max(1, r.Value)}단계");
                        break;
                    case UnlockKind.DayAtLeast: text.Append($"{r.Value}일차부터"); break;
                    case UnlockKind.FirstHarvest:
                        var crop = database.Get<CropData>(r.TargetId);
                        text.Append($"{(crop != null ? crop.DisplayName : r.TargetId)} 첫 수확");
                        break;
                }
            }
            return text.Length > 0 ? text + " 필요" : string.Empty;
        }

        // ── 구매 ─────────────────────────────────────────

        /// <summary>지금 단계에서 살 수 있는지 (아니면 상점은 보기만).</summary>
        public bool CanBuyNow
        {
            get
            {
                var game = GameManager.Instance;
                return game != null && game.IsStarted && buyPhases.Contains(game.Phase);
            }
        }

        public string ViewOnlyMessage => viewOnlyMessage;

        /// <summary>튜토리얼(5-7)이 이 상품을 잠갔는지 — 안내한 아이템을 주는 상품만 살 수 있는 단계.</summary>
        public bool IsTutorialLocked(ShopProductData product) =>
            Systems.Tutorial.TutorialSystem.IsLockedFor(Logic.Tutorial.TutorialLocks.BuyOther, product is ItemProductData ip && ip.Item != null ? ip.Item.Id : null);

        /// <summary>산다. 결과 문구(화면 안내 한 줄)를 돌려준다.</summary>
        public bool TryBuy(ShopProductData product, out string message)
        {
            var game = GameManager.Instance;
            if (product == null || game == null || !game.IsStarted)
            {
                message = "지금은 살 수 없음";
                return false;
            }
            if (!CanBuyNow)
            {
                message = viewOnlyMessage;
                return false;
            }
            if (IsTutorialLocked(product))
            {
                message = tutorialLockMessage;
                return false;
            }
            var spec = product.ToSpec();
            var offer = state.Offer(spec, game, game.Money);
            string label = product.TierLabel(offer.Tier);
            switch (offer.Status)
            {
                case OfferStatus.Locked: message = $"{label} - 잠김 ({LockReason(product, offer.Tier)})"; return false;
                case OfferStatus.SoldOut: message = $"{label} - 오늘 입고가 다 팔림"; return false;
                case OfferStatus.MaxedOut: message = $"{product.DisplayName} - 이미 최대 단계"; return false;
                case OfferStatus.NotEnoughMoney: message = $"{label} - 달러 부족 ({offer.Price}달러 필요)"; return false;
            }
            // 효과를 줄 수 있는지 먼저 본다 — 돈만 빠지고 못 받는 일이 없게
            if (!CanApply(product, offer.Tier, out string reason))
            {
                message = $"{label} - {reason}";
                return false;
            }
            if (!state.TryBuy(spec, game, game.Money, out offer) || !game.TryChangeMoney(-offer.Price))
            {
                message = $"{label} - 살 수 없음";
                return false;
            }
            message = Apply(product, offer.Tier);
            spentToday = state.SpentToday;
            NotifyChanged();
            return true;
        }

        private bool CanApply(ShopProductData product, int tier, out string reason)
        {
            reason = string.Empty;
            switch (product)
            {
                case ItemProductData item:
                    if (item.Item == null) { reason = "상품 데이터에 아이템이 없음"; return false; }
                    if (!inventory.CanAdd(item.Item, item.Count)) { reason = "넣을 칸 없음"; return false; }
                    return true;
                case ToolUpgradeProductData tool:
                    var next = tool.ToolAt(tier);
                    if (next == null) { reason = "상품 데이터에 등급 도구가 없음"; return false; }
                    if (FindReplaceable(tool, tier) != null || inventory.CanAdd(next, 1))
                        return true;
                    reason = "넣을 칸 없음";
                    return false;
                case PlotExpandProductData plot:
                    if (Farm != null && Farm.PlotCount + plot.PlotsAt(tier) <= Farm.MaxPlotCount)
                        return true;
                    reason = "밭을 더 넓힐 수 없음";
                    return false;
                case BagExpandProductData bag:
                    if (inventory.Bag != null && inventory.Bag.SlotCount + bag.Slots <= inventory.Settings.BagMaxSlots)
                        return true;
                    reason = "가방을 더 늘릴 수 없음";
                    return false;
                case ShelfExpandProductData _:
                    if (Shelves.Count > 0 && Shelves[0] != null)
                        return true;
                    reason = "진열대가 없음";
                    return false;
                default:
                    reason = "알 수 없는 상품 종류";
                    return false;
            }
        }

        private string Apply(ShopProductData product, int tier)
        {
            switch (product)
            {
                case ItemProductData item:
                    inventory.TryAdd(item.Item, item.Count);
                    return $"{item.Item.DisplayName} ×{item.Count} 삼";
                case ToolUpgradeProductData tool:
                    var next = tool.ToolAt(tier);
                    var old = FindReplaceable(tool, tier);
                    if (old != null && (inventory.TryReplaceOne(old.Id, next) || storage.TryReplaceOne(old.Id, next)))
                        return $"{old.DisplayName} → {next.DisplayName}";
                    inventory.TryAdd(next, 1);
                    return $"{next.DisplayName} 삼";
                case PlotExpandProductData plot:
                    Farm.TryAddPlots(plot.PlotsAt(tier));
                    return $"밭 구획 +{plot.PlotsAt(tier)} (지금 {Farm.PlotCount}구획)";
                case BagExpandProductData bag:
                    inventory.SetBagSlotCount(inventory.Bag.SlotCount + bag.Slots);
                    return $"가방 +{bag.Slots}칸 (지금 {inventory.Bag.SlotCount}칸)";
                case ShelfExpandProductData shelf:
                    var display = Shelves[0];
                    display.SetSlotCount(display.State.SlotCount + shelf.Slots);
                    return $"진열 칸 +{shelf.Slots} (지금 {display.State.SlotCount}칸)";
                default:
                    return product.DisplayName;
            }
        }

        // 인벤토리(핫바 · 가방 · 손에 든 묶음) → 창고 순서로 교체할 앞 등급 도구를 찾는다.
        private ToolData FindReplaceable(ToolUpgradeProductData product, int tier)
        {
            foreach (var t in product.ReplaceableBefore(tier))
                if (inventory.CountOf(t) > 0 || inventory.Cursor.ItemId == t.Id)
                    return t;
            foreach (var t in product.ReplaceableBefore(tier))
                if (storage.CountOf(t.Id) > 0)
                    return t;
            return null;
        }

        private void NotifyChanged()
        {
            UpdateDebugView();
            Changed?.Invoke();
        }

        private void UpdateDebugView()
        {
            spentToday = state.SpentToday;
            offersView.Clear();
            foreach (var p in products)
            {
                var o = OfferOf(p);
                offersView.Add($"{p.Id}: {o.Status} 단계 {o.Tier} · {o.Price}달러 · 남음 {(o.StockLeft < 0 ? "무제한" : o.StockLeft.ToString())}{(o.Visible ? "" : " (숨김)")}");
            }
        }
    }
}
