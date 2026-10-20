using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Logic.Inventory;
using NevrianGlowFarm.Systems.Player;
using NevrianGlowFarm.Systems.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 창고 화면 (창고 접근 지점에서 E, GDD 4-3): 왼쪽 창고 목록을 누르면 그 종류를 한 묶음 꺼내고,
    /// 오른쪽 가방 · 핫바 칸을 누르면 그 칸을 통째로 넣는다. "전부 넣기" = 도구를 뺀 보관 가능 아이템 전부.
    /// 쓰레기 등 보관 불가 · 자리 없음은 안내 한 줄로 알린다. Tab · Esc로 닫는다.
    /// </summary>
    public class StorageScreen : GameScreen
    {
        [Header("참조")]
        [Tooltip("인벤토리 (Core Player)")]
        [SerializeField] private PlayerInventory inventory;
        [Tooltip("창고 (Core Storage)")]
        [SerializeField] private StorageSystem storage;
        [Tooltip("가방 칸")]
        [SerializeField] private SlotGridView bagGrid;
        [Tooltip("핫바 칸")]
        [SerializeField] private SlotGridView hotbarGrid;
        [Tooltip("창고 목록 줄 원본 — 비활성 자식으로 두면 종류 수만큼 복제한다")]
        [SerializeField] private HotbarSlotView entryTemplate;
        [Tooltip("창고 목록을 늘어놓을 부모")]
        [SerializeField] private Transform entryParent;
        [Tooltip("전부 넣기 버튼")]
        [SerializeField] private Button depositAllButton;
        [Tooltip("제목 (창고 이름)")]
        [SerializeField] private TMP_Text titleLabel;
        [Tooltip("안내 한 줄")]
        [SerializeField] private TMP_Text messageLabel;
        [Tooltip("닫기 입력 (Player/Inventory, Tab)")]
        [SerializeField] private InputActionReference toggleAction;
        [Tooltip("닫기 입력 (UI/Cancel, Esc)")]
        [SerializeField] private InputActionReference cancelAction;

        [Header("조정값")]
        [Tooltip("창고 목록 줄 색")]
        [SerializeField] private Color entryColor = new Color(0.05f, 0.15f, 0.25f, 0.85f);
        [Tooltip("창고가 비었을 때 글자")]
        [SerializeField] private string emptyText = "창고가 비어 있음";

        private readonly List<HotbarSlotView> entries = new List<HotbarSlotView>();
        private readonly List<string> entryIds = new List<string>();

        public static StorageScreen Instance { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (inventory == null || storage == null || bagGrid == null || hotbarGrid == null || entryTemplate == null || entryParent == null
                || depositAllButton == null || titleLabel == null || messageLabel == null || toggleAction == null || cancelAction == null)
            {
                Debug.LogError("[StorageScreen] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            Instance = this;
            entryTemplate.gameObject.SetActive(false);
            bagGrid.SlotClicked += OnInventorySlotClicked;
            hotbarGrid.SlotClicked += OnInventorySlotClicked;
            depositAllButton.onClick.AddListener(DepositAll);
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
            storage.Changed += RebuildEntries;
        }

        private void OnDisable()
        {
            storage.Changed -= RebuildEntries;
        }

        private void Update()
        {
            if (IsOpen && (toggleAction.action.WasPressedThisFrame() || cancelAction.action.WasPressedThisFrame()))
                Close();
        }

        /// <summary>창고 화면을 연다 (접근 지점이 부른다).</summary>
        public void Open(string title)
        {
            titleLabel.text = title;
            Show();
        }

        protected override void OnOpened()
        {
            messageLabel.text = string.Empty;
            RebuildEntries();
            AudioManager.Play(SoundCues.StorageOpen);
        }

        /// <summary>인벤토리 칸 하나를 넣는다 (화면 클릭 · 디버그).</summary>
        public void OnInventorySlotClicked(SlotInventory slots, int index)
        {
            if (!IsOpen)
                return;
            var stack = slots[index];
            if (stack.IsEmpty)
            {
                messageLabel.text = string.Empty;
                return;
            }
            var item = inventory.ItemOf(stack);
            if (!storage.CanStore(stack.ItemId))
            {
                messageLabel.text = $"{(item != null ? item.DisplayName : stack.ItemId)}은(는) 창고에 넣을 수 없음";
                return;
            }
            if (storage.DepositSlot(inventory, slots, index))
                AudioManager.Play(SoundCues.StoragePut);
            messageLabel.text = $"{ItemLabel.Of(inventory, stack, string.Empty)} 넣음";
        }

        /// <summary>창고 목록 줄 하나를 한 묶음 꺼낸다 (화면 클릭 · 디버그).</summary>
        public void OnEntryClicked(string itemId)
        {
            if (!IsOpen)
                return;
            int taken = storage.WithdrawStack(inventory, itemId);
            var item = inventory.Lookup(itemId);
            string name = item != null ? item.DisplayName : itemId;
            messageLabel.text = taken > 0 ? $"{name} ×{taken} 꺼냄" : $"{name} - 넣을 칸 없음";
        }

        /// <summary>도구를 뺀 보관 가능 아이템을 모두 넣는다.</summary>
        public void DepositAll()
        {
            if (!IsOpen)
                return;
            int moved = storage.DepositAll(inventory);
            if (moved > 0)
                AudioManager.Play(SoundCues.StoragePut);
            messageLabel.text = moved > 0 ? $"{moved}칸 넣음 (도구 · 보관 불가 아이템은 남김)" : "넣을 아이템이 없음";
        }

        private void RebuildEntries()
        {
            var list = storage.Entries;
            entryIds.Clear();

            int rows = Mathf.Max(list.Count, 1);
            while (entries.Count < rows)
            {
                int row = entries.Count;
                var view = Instantiate(entryTemplate, entryParent);
                view.name = $"Entry{row + 1}";
                view.Clicked += () => { if (row < entryIds.Count) OnEntryClicked(entryIds[row]); };
                entries.Add(view);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                bool used = i < rows;
                entries[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                entries[i].SetSelected(false, entryColor, entryColor);
                if (list.Count == 0)
                {
                    entries[i].SetContent(0, emptyText);
                    continue;
                }
                var e = list[i];
                var item = inventory.Lookup(e.ItemId);
                entries[i].SetContent(0, $"{(item != null ? item.DisplayName : e.ItemId)} ×{e.Count}");
                entryIds.Add(e.ItemId);
            }
        }
    }
}
