using NevrianGlowFarm.Core;
using NevrianGlowFarm.Logic.Inventory;
using NevrianGlowFarm.Systems.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NevrianGlowFarm.UI
{
    /// <summary>
    /// 가방 화면 (Tab, GDD 4-3): 가방 · 핫바 칸을 클릭해 집기 · 놓기 · 바꾸기 · 합치기. 손에 든 묶음은 마우스를 따라다닌다.
    /// 닫을 때 손에 든 묶음은 인벤토리로 돌아간다 (같은 묶음 → 빈 칸).
    /// </summary>
    public class InventoryScreen : GameScreen
    {
        [Header("참조")]
        [Tooltip("인벤토리 (Core Player)")]
        [SerializeField] private PlayerInventory inventory;
        [Tooltip("가방 칸")]
        [SerializeField] private SlotGridView bagGrid;
        [Tooltip("핫바 칸")]
        [SerializeField] private SlotGridView hotbarGrid;
        [Tooltip("손에 든 묶음 표시 (마우스를 따라감)")]
        [SerializeField] private TMP_Text cursorLabel;
        [Tooltip("안내 한 줄 (자리 없음 등)")]
        [SerializeField] private TMP_Text messageLabel;
        [Tooltip("열기 · 닫기 입력 (Player/Inventory, Tab)")]
        [SerializeField] private InputActionReference toggleAction;
        [Tooltip("닫기 입력 (UI/Cancel, Esc)")]
        [SerializeField] private InputActionReference cancelAction;

        [Header("조정값")]
        [Tooltip("손에 든 묶음 글자를 마우스에서 띄우는 거리 (px)")]
        [SerializeField] private Vector2 cursorOffset = new Vector2(18f, -18f);

        public static InventoryScreen Instance { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (inventory == null || bagGrid == null || hotbarGrid == null || cursorLabel == null || messageLabel == null || toggleAction == null || cancelAction == null)
            {
                Debug.LogError("[InventoryScreen] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            Instance = this;
            bagGrid.SlotClicked += OnSlotClicked;
            hotbarGrid.SlotClicked += OnSlotClicked;
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
            inventory.Changed += RefreshCursor;
        }

        private void OnDisable()
        {
            inventory.Changed -= RefreshCursor;
        }

        private void Update()
        {
            if (IsOpen)
            {
                if (toggleAction.action.WasPressedThisFrame() || cancelAction.action.WasPressedThisFrame())
                    Close();
                else
                    FollowMouse();
                return;
            }

            // 다른 화면이 열려 있거나 이번 프레임에 닫혔으면 Tab으로 열지 않는다.
            if (Current == null && !ClosedThisFrame && toggleAction.action.WasPressedThisFrame())
                Open();
        }

        public void Open() => Show();

        protected override void OnOpened()
        {
            messageLabel.text = string.Empty;
            RefreshCursor();
        }

        protected override bool OnClosing()
        {
            if (inventory.ReturnCursor())
                return true;
            messageLabel.text = "손에 든 아이템을 놓을 칸이 없음 — 빈 칸에 놓은 뒤 닫기";
            return false;
        }

        private void OnSlotClicked(SlotInventory slots, int index)
        {
            if (!IsOpen)
                return;
            messageLabel.text = string.Empty;
            inventory.ClickSlot(slots, index);
        }

        private void RefreshCursor()
        {
            var cursor = inventory.Cursor;
            cursorLabel.gameObject.SetActive(IsOpen && !cursor.IsEmpty);
            if (!cursor.IsEmpty)
                cursorLabel.text = ItemLabel.Of(inventory, cursor, string.Empty);
            FollowMouse();
        }

        private void FollowMouse()
        {
            if (!cursorLabel.gameObject.activeSelf || Mouse.current == null)
                return;
            cursorLabel.rectTransform.position = Mouse.current.position.ReadValue() + cursorOffset;
        }
    }
}
