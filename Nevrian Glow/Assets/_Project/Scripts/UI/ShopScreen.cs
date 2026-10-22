using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Shop;
using NevrianGlowFarm.Systems.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 상점 화면 (B, 어디서나 — GDD 3장 화면 표). 분류별 탭(P-7, 와이어프레임 4) 안에 보이는 상품을 한 줄씩: 이름(단계) · 가격 · 오늘 남은 입고 · 상태.
    /// 상점 · 농사 단계가 아니면 보기만 (회신2 4절 #2 — ShopSystem.CanBuyNow).
    /// 줄을 누르면 산다. 잠긴 단계는 조건을 함께 적는다. B · Esc로 닫는다. 모양은 UI를 몰아서 다듬을 때까지 그레이박스.
    /// 하루 시작(상점 단계)에는 저절로 열리고, 그때 닫으면 농사 단계로 넘어간다 (3-4).
    /// </summary>
    public class ShopScreen : GameScreen
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

        [Header("조정값")]
        [Tooltip("탭 = 상품 분류(ShopProductData.category) 순서 (와이어프레임 4: 씨앗 · 도구 · 확장 · 가구 · 소모품 · 장치). 목록에 없는 분류는 마지막 탭에")]
        [SerializeField] private List<string> tabs = new List<string> { "씨앗", "도구", "확장", "가구", "소모품 · 장치" };
        [Tooltip("탭에 보이는 상품이 없을 때 안내 (결정 2026-10-21, 사용자)")]
        [SerializeField] private string emptyTabMessage = "데모버전에서는 해금되지 않은 컨텐츠입니다.";
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
        }

        private void OnDisable()
        {
            shop.Changed -= Rebuild;
            GameEvents.PhaseChanged -= OnPhaseChanged;
        }

        // 하루 시작(상점 단계)이면 저절로 연다 — 닫으면 농사 단계로 (GDD 4-1 "상점: 닫기", 결정 2026-10-11)
        private void OnPhaseChanged(Logic.Game.PhaseChange change) => openForDayStart = change.Phase == Logic.Game.DayPhase.Shop;

        private bool openForDayStart;

        private void Update()
        {
            if (IsOpen)
            {
                if (toggleAction.action.WasPressedThisFrame() || cancelAction.action.WasPressedThisFrame())
                    Close();
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
            titleLabel.text = game == null ? "상점"
                : game.Phase == Logic.Game.DayPhase.Shop ? $"{game.Day}일차 상점 - 보유 {game.Money:N0}달러 · 닫으면 농사 시작"
                : canBuy ? $"상점 - 보유 {game.Money:N0}달러"
                : $"상점 (보기만) - 보유 {game.Money:N0}달러";

            bool useTabs = tabViews.Count > 0;
            for (int i = 0; i < tabViews.Count; i++)
                tabViews[i].SetSelected(i == tabIndex, tabNormalColor, tabSelectedColor);

            rowProducts.Clear();
            foreach (var p in shop.Products)
                if (shop.OfferOf(p).Visible && (!useTabs || TabOf(p) == tabIndex))
                    rowProducts.Add(p);
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
                entries[i].SetContent(0, RowText(product, offer, useTabs));
                var color = offer.Status == OfferStatus.Available && canBuy ? availableColor : unavailableColor;
                entries[i].SetSelected(false, color, color);
            }
        }

        // 게임 화면 글자에는 "—"를 쓰지 않는다 (글꼴에 없음, TASKS 트러블슈팅)
        private string RowText(ShopProductData product, ProductOffer offer, bool tabbed)
        {
            // 탭이 있으면 분류는 탭이 보여 준다
            string head = tabbed || string.IsNullOrEmpty(product.Category) ? string.Empty : $"[{product.Category}] ";
            if (offer.Status == OfferStatus.MaxedOut)
                return $"{head}{product.DisplayName} - 최대 단계";
            string stock = offer.StockLeft < 0 ? string.Empty : $" · 오늘 {offer.StockLeft}개";
            string text = $"{head}{product.TierLabel(offer.Tier)} - {offer.Price:N0}달러{stock}";
            switch (offer.Status)
            {
                case OfferStatus.Locked: return $"{text} · 잠김 ({shop.LockReason(product, offer.Tier)})";
                case OfferStatus.SoldOut:
                    bool firstDay = product.StockFromNextDay && GameManager.Instance != null && shop.State.FirstOpenDay(product.Id) >= GameManager.Instance.Day;
                    return $"{text} · {(firstDay ? "내일부터 입고" : "오늘 품절")}";
                case OfferStatus.NotEnoughMoney: return $"{text} · 달러 부족";
                default: return text;
            }
        }
    }
}
