using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Shop;
using NevrianGlowFarm.Logic.Text;
using NevrianGlowFarm.Systems.Player;
using NevrianGlowFarm.Systems.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 상점 화면 (B, 어디서나 — GDD 3장 화면 표). 분류별 탭(P-7, 와이어프레임 4) 안에 보이는 상품을 한 줄씩 (5-4c, 스타듀밸리식):
    /// [아이콘] 이름(단계) / 둘째 줄 상태(오늘 남은 입고 · 잠김 조건 · 보유) … 가격 [달러]. 마우스를 올리면 설명 툴팁.
    /// 상점 · 농사 단계가 아니면 보기만 (회신2 4절 #2 — ShopSystem.CanBuyNow).
    /// 줄을 누르면 산다. 잠긴 단계는 조건을 함께 적는다. B · Esc로 닫는다.
    /// 하루 시작(상점 단계)에는 저절로 열리고, 그때 닫으면 농사 단계로 넘어간다 (3-4).
    /// </summary>
    public class ShopScreen : GameScreen, Systems.Tutorial.ITutorialHighlightSource
    {
        [Header("참조")]
        [Tooltip("상점 (Core Shop)")]
        [SerializeField] private ShopSystem shop;
        [Tooltip("상품 줄 원본 — 비활성 자식으로 두면 상품 수만큼 복제한다")]
        [SerializeField] private HotbarSlotView entryTemplate;
        [Tooltip("상품 줄을 늘어놓을 부모")]
        [SerializeField] private Transform entryParent;
        [Tooltip("제목 (보유 달러)")]
        [SerializeField] private TMP_Text titleLabel;
        [Tooltip("안내 한 줄")]
        [SerializeField] private TMP_Text messageLabel;
        [Tooltip("열기 · 닫기 입력 (Player/Shop, B)")]
        [SerializeField] private InputActionReference toggleAction;
        [Tooltip("닫기 입력 (UI/Cancel, Esc)")]
        [SerializeField] private InputActionReference cancelAction;
        [Tooltip("탭 버튼 원본 — 비활성 자식으로 두면 탭 수만큼 복제한다 (P-7)")]
        [SerializeField] private HotbarSlotView tabTemplate;
        [Tooltip("탭 버튼을 늘어놓을 부모")]
        [SerializeField] private Transform tabParent;
        [Tooltip("탭에 상품이 없을 때 보이는 글자")]
        [SerializeField] private TMP_Text emptyLabel;
        [Tooltip("인벤토리 (Core Player) — 상품이 주는 아이템의 보유 수를 적는다 (비우면 안 적음, 5-4c)")]
        [SerializeField] private PlayerInventory inventory;
        [Tooltip("가격 옆 달러 아이콘 (비우면 숫자만, 5-4c)")]
        [SerializeField] private Sprite priceIcon;
        [Tooltip("보유 달러 글자 — 제목 오른쪽에 따로 (비우면 제목에 같이, 5-4c)")]
        [SerializeField] private TMP_Text moneyLabel;

        [Header("조정값")]
        [Tooltip("탭 = 상품 분류(ShopProductData.category) 순서 (와이어프레임 4: 씨앗 · 도구 · 확장 · 가구 · 소모품 · 장치). 목록에 없는 분류는 마지막 탭에")]
        [SerializeField] private List<string> tabs = new List<string> { "씨앗", "도구", "확장", "가구", "소모품 · 장치" };
        [Tooltip("탭에 보이는 상품이 없을 때 안내 (결정 2026-10-21, 사용자)")]
        [SerializeField] private string emptyTabMessage = "데모버전에서는 해금되지 않은 컨텐츠입니다.";
        [Tooltip("튜토리얼이 닫기를 잠갔을 때 안내 (5-7 close_shop, 게임 화면 글자 - '—' 쓰지 않음)")]
        [SerializeField] private string tutorialCloseMessage = "먼저 안내한 것을 사고 닫아요";
        [Tooltip("살 수 있는 줄 색")]
        [SerializeField] private Color availableColor = new Color(0.05f, 0.25f, 0.3f, 0.9f);
        [Tooltip("살 수 없는 줄 색 (잠김 · 품절 · 달러 부족 · 최대 · 보기만)")]
        [SerializeField] private Color unavailableColor = new Color(0.12f, 0.12f, 0.16f, 0.85f);
        [Tooltip("고른 탭 색")]
        [SerializeField] private Color tabSelectedColor = new Color(0.15f, 0.45f, 0.5f, 0.95f);
        [Tooltip("고르지 않은 탭 색")]
        [SerializeField] private Color tabNormalColor = new Color(0.08f, 0.14f, 0.18f, 0.9f);

        private readonly List<HotbarSlotView> entries = new List<HotbarSlotView>();
        private readonly List<ShopProductData> rowProducts = new List<ShopProductData>();
        private readonly List<HotbarSlotView> tabViews = new List<HotbarSlotView>();
        private int tabIndex;

        public static ShopScreen Instance { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (shop == null || entryTemplate == null || entryParent == null || titleLabel == null || messageLabel == null || toggleAction == null || cancelAction == null)
            {
                Debug.LogError("[ShopScreen] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            Instance = this;
            entryTemplate.gameObject.SetActive(false);
            BuildTabs();
        }

        // 탭 버튼은 한 번만 만든다 (탭 목록은 조정값). 탭 UI가 없으면 예전처럼 한 목록.
        private void BuildTabs()
        {
            if (tabTemplate == null || tabParent == null || tabs.Count == 0)
                return;
            tabTemplate.gameObject.SetActive(false);
            for (int i = 0; i < tabs.Count; i++)
            {
                int index = i;
                var view = Instantiate(tabTemplate, tabParent);
                view.name = $"Tab{i + 1}";
                view.gameObject.SetActive(true);
                view.SetContent(0, tabs[i]);
                view.Clicked += () => SelectTab(index);
                tabViews.Add(view);
            }
        }

        public int TabCount => tabViews.Count;

        /// <summary>탭을 고른다 (탭 클릭 · 디버그).</summary>
        public void SelectTab(int index)
        {
            if (index < 0 || index >= tabViews.Count)
                return;
            tabIndex = index;
            messageLabel.text = string.Empty;
            Rebuild();
        }

        // 상품의 탭 = 분류가 같은 탭, 목록에 없는 분류는 마지막 탭
        private int TabOf(ShopProductData product)
        {
            int i = tabs.IndexOf(product.Category ?? string.Empty);
            return i >= 0 ? i : tabs.Count - 1;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
                Instance = null;
        }

        private void OnEnable()
        {
            toggleAction.action.Enable();
            cancelAction.action.Enable();
            shop.Changed += Rebuild;
            GameEvents.PhaseChanged += OnPhaseChanged;
            if (inventory != null)
                inventory.Changed += Rebuild;
            Systems.Tutorial.TutorialHighlights.Register(this);
        }

        private void OnDisable()
        {
            shop.Changed -= Rebuild;
            GameEvents.PhaseChanged -= OnPhaseChanged;
            if (inventory != null)
                inventory.Changed -= Rebuild;
            Systems.Tutorial.TutorialHighlights.Unregister(this);
        }

        // 튜토리얼 강조 (5-7): shop_tab_seed = 씨앗 상품이 있는 탭, shop_row:<아이템 id> = 그 아이템을 주는 상품 줄 (열려 있을 때만)
        public bool TryGetHighlight(string name, string arg, out Systems.Tutorial.HighlightHit hit)
        {
            hit = default;
            if (!IsOpen)
                return false;
            if (name == "shop_tab_seed" && tabViews.Count > 0)
            {
                foreach (var p in shop.Products)
                    if (p is ItemProductData ip && ip.Item is SeedData)
                    {
                        hit = Systems.Tutorial.HighlightHit.On(tabViews[TabOf(p)].GetComponent<RectTransform>());
                        return true;
                    }
                return false;
            }
            if (name == "shop_row" && !string.IsNullOrEmpty(arg))
            {
                for (int i = 0; i < rowProducts.Count && i < entries.Count; i++)
                    if (rowProducts[i] is ItemProductData ip && ip.Item != null && ip.Item.Id == arg)
                    {
                        hit = Systems.Tutorial.HighlightHit.On(entries[i].GetComponent<RectTransform>());
                        return true;
                    }
            }
            return false;
        }

        // 하루 시작(상점 단계)이면 저절로 연다 — 닫으면 농사 단계로 (GDD 4-1 "상점: 닫기", 결정 2026-10-11)
        private void OnPhaseChanged(Logic.Game.PhaseChange change) => openForDayStart = change.Phase == Logic.Game.DayPhase.Shop;

        private bool openForDayStart;

        private void Update()
        {
            if (IsOpen)
            {
                if (toggleAction.action.WasPressedThisFrame() || cancelAction.action.WasPressedThisFrame())
                {
                    // 튜토리얼(5-7): 안내한 것을 사기 전에는 닫히지 않는다
                    if (Systems.Tutorial.TutorialSystem.IsLocked(Logic.Tutorial.TutorialLocks.CloseShop))
                    {
                        messageLabel.text = tutorialCloseMessage;
                        AudioManager.Play(SoundCues.UiBuyFail);
                    }
                    else
                        Close();
                }
                return;
            }
            if (openForDayStart && Current == null)
            {
                openForDayStart = false;
                Open();
                return;
            }
            // 다른 화면이 열려 있거나 이번 프레임에 닫혔으면 열지 않는다.
            if (Current == null && !ClosedThisFrame && toggleAction.action.WasPressedThisFrame())
                Open();
        }

        public void Open() => Show();

        protected override void OnOpened()
        {
            messageLabel.text = shop.CanBuyNow ? string.Empty : shop.ViewOnlyMessage;
            Rebuild();
        }

        protected override void OnClosed()
        {
            var game = GameManager.Instance;
            if (game != null && game.IsStarted && game.Phase == Logic.Game.DayPhase.Shop)
                game.AdvancePhase(); // 상점 → 농사
        }

        /// <summary>상품 줄 하나를 산다 (화면 클릭 · 디버그).</summary>
        public void OnEntryClicked(ShopProductData product)
        {
            if (!IsOpen || product == null)
                return;
            bool bought = shop.TryBuy(product, out string message);
            AudioManager.Play(bought ? SoundCues.UiBuySuccess : SoundCues.UiBuyFail);
            messageLabel.text = message;
        }

        private void Rebuild()
        {
            if (!IsOpen)
                return;
            var game = GameManager.Instance;
            bool canBuy = shop.CanBuyNow;
            // 보유 달러 글자가 따로 있으면 제목은 짧게 (오른쪽 위에 "150 [달러]")
            string money = moneyLabel != null || game == null ? string.Empty : $" - 보유 {game.Money:N0}달러";
            titleLabel.text = game == null ? "상점"
                : game.Phase == Logic.Game.DayPhase.Shop ? $"{game.Day}일차 상점{money} · 닫으면 농사 시작"
                : canBuy ? $"상점{money}"
                : $"상점 (보기만){money}";
            if (moneyLabel != null)
                moneyLabel.text = game != null ? $"{game.Money:N0}" : string.Empty;

            bool useTabs = tabViews.Count > 0;
            for (int i = 0; i < tabViews.Count; i++)
                tabViews[i].SetSelected(i == tabIndex, tabNormalColor, tabSelectedColor);

            // 잠긴 상품도 "잠김 · 무엇이 필요"로 보인다 (기획 회신 10/22 A6) — 숨김 상품(2차 · 연결 전)은 ShopSystem이 이미 뺐다
            rowProducts.Clear();
            foreach (var p in shop.Products)
            {
                var offer = shop.OfferOf(p);
                if ((offer.Visible || offer.Status == OfferStatus.Locked) && (!useTabs || TabOf(p) == tabIndex))
                    rowProducts.Add(p);
            }
            if (emptyLabel != null)
            {
                emptyLabel.gameObject.SetActive(rowProducts.Count == 0);
                emptyLabel.text = emptyTabMessage;
            }

            while (entries.Count < rowProducts.Count)
            {
                int row = entries.Count;
                var view = Instantiate(entryTemplate, entryParent);
                view.name = $"Product{row + 1}";
                view.Clicked += () => { if (row < rowProducts.Count) OnEntryClicked(rowProducts[row]); };
                entries.Add(view);
            }
            for (int i = 0; i < entries.Count; i++)
            {
                bool used = i < rowProducts.Count;
                entries[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                var product = rowProducts[i];
                var offer = shop.OfferOf(product);
                var view = entries[i];
                view.SetIcon(product.IconFor(offer.Tier));
                // 탭이 있으면 분류는 탭이 보여 준다
                string head = useTabs || string.IsNullOrEmpty(product.Category) ? string.Empty : $"[{product.Category}] ";
                view.SetContent(0, head + product.TierLabel(offer.Tier));
                view.SetDetail(UiText.Join(UiText.Offer(offer.Status, offer.StockLeft, LockReason(product, offer), StockFromTomorrow(product)), UiText.Owned(OwnedCount(product))));
                view.SetValue(offer.Status == OfferStatus.MaxedOut ? string.Empty : $"{offer.Price:N0}", priceIcon);
                view.BindTooltip(() => Tooltip(product));
                bool available = offer.Status == OfferStatus.Available && canBuy && !shop.IsTutorialLocked(product);
                var color = available ? availableColor : unavailableColor;
                view.SetSelected(false, color, color);
                view.SetDimmed(!available);
            }
        }

        private string LockReason(ShopProductData product, ProductOffer offer) =>
            offer.Status == OfferStatus.Locked ? shop.LockReason(product, offer.Tier) : string.Empty;

        // 처음 열린 날은 입고 없이 다음 날부터 (BALANCE 3-8) — 품절과 구분해 "내일부터 입고"
        private bool StockFromTomorrow(ShopProductData product) =>
            product.StockFromNextDay && GameManager.Instance != null && shop.State.FirstOpenDay(product.Id) >= GameManager.Instance.Day;

        // 아이템 상품만 보유 수를 적는다 (도구 · 확장은 보유 개념이 없음)
        private int OwnedCount(ShopProductData product) =>
            inventory != null && product is ItemProductData ip && ip.Item != null ? inventory.CountOf(ip.Item) : 0;

        // 툴팁은 마우스를 올린 순간의 값으로 (산 뒤 보유 수 · 남은 입고가 바뀐다)
        private TooltipContent Tooltip(ShopProductData product)
        {
            var offer = shop.OfferOf(product);
            return TooltipText.ForProduct(product, offer, LockReason(product, offer), StockFromTomorrow(product), OwnedCount(product), shop.CanBuyNow);
        }
    }
}
