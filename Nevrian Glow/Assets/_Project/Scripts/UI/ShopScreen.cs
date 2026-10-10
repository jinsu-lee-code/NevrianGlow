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
    /// 상점 화면 (B, 어디서나 — GDD 3장 화면 표). 보이는 상품을 한 줄씩: 분류 · 이름(단계) · 가격 · 오늘 남은 입고 · 상태.
    /// 줄을 누르면 산다. 잠긴 단계는 조건을 함께 적는다. B · Esc로 닫는다. 모양은 UI를 몰아서 다듬을 때까지 그레이박스.
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

        [Header("조정값")]
        [Tooltip("살 수 있는 줄 색")]
        [SerializeField] private Color availableColor = new Color(0.05f, 0.25f, 0.3f, 0.9f);
        [Tooltip("살 수 없는 줄 색 (잠김 · 품절 · 달러 부족 · 최대)")]
        [SerializeField] private Color unavailableColor = new Color(0.12f, 0.12f, 0.16f, 0.85f);

        private readonly List<HotbarSlotView> entries = new List<HotbarSlotView>();
        private readonly List<ShopProductData> rowProducts = new List<ShopProductData>();

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
        }

        private void OnDisable() => shop.Changed -= Rebuild;

        private void Update()
        {
            if (IsOpen)
            {
                if (toggleAction.action.WasPressedThisFrame() || cancelAction.action.WasPressedThisFrame())
                    Close();
                return;
            }
            // 다른 화면이 열려 있거나 이번 프레임에 닫혔으면 열지 않는다.
            if (Current == null && !ClosedThisFrame && toggleAction.action.WasPressedThisFrame())
                Open();
        }

        public void Open() => Show();

        protected override void OnOpened()
        {
            messageLabel.text = string.Empty;
            Rebuild();
        }

        /// <summary>상품 줄 하나를 산다 (화면 클릭 · 디버그).</summary>
        public void OnEntryClicked(ShopProductData product)
        {
            if (!IsOpen || product == null)
                return;
            shop.TryBuy(product, out string message);
            messageLabel.text = message;
        }

        private void Rebuild()
        {
            if (!IsOpen)
                return;
            var game = GameManager.Instance;
            titleLabel.text = game != null ? $"상점 - 보유 {game.Money:N0}달러" : "상점";

            rowProducts.Clear();
            foreach (var p in shop.Products)
                if (shop.OfferOf(p).Visible)
                    rowProducts.Add(p);

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
                entries[i].SetContent(0, RowText(product, offer));
                var color = offer.Status == OfferStatus.Available ? availableColor : unavailableColor;
                entries[i].SetSelected(false, color, color);
            }
        }

        // 게임 화면 글자에는 "—"를 쓰지 않는다 (글꼴에 없음, TASKS 트러블슈팅)
        private string RowText(ShopProductData product, ProductOffer offer)
        {
            string head = string.IsNullOrEmpty(product.Category) ? string.Empty : $"[{product.Category}] ";
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
