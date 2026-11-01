using System;
using System.Collections.Generic;
using NevrianGlowFarm.Logic.Inventory;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;

namespace NevrianGlowFarm.UI
{
    /// <summary>인벤토리의 어느 칸 묶음을 그릴지.</summary>
    public enum InventoryPart
    {
        Hotbar,
        Bag,
    }

    /// <summary>
    /// 핫바 또는 가방의 칸을 늘어놓고 클릭을 알린다 (가방 · 창고 화면). 칸 수만큼 칸을 만들고, 인벤토리가 바뀔 때만 다시 그린다.
    /// 칸에 마우스를 올리면 아이템 설명 툴팁 (5-4c).
    /// </summary>
    public class SlotGridView : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("그릴 인벤토리 (Core Player)")]
        [SerializeField] private PlayerInventory inventory;
        [Tooltip("핫바 / 가방")]
        [SerializeField] private InventoryPart part;
        [Tooltip("칸 원본 — 비활성 자식으로 두면 칸 수만큼 복제한다 (버튼 있는 칸)")]
        [SerializeField] private HotbarSlotView slotTemplate;
        [Tooltip("칸을 늘어놓을 부모 (GridLayoutGroup / HorizontalLayoutGroup)")]
        [SerializeField] private Transform slotParent;

        [Header("조정값")]
        [Tooltip("칸 색")]
        [SerializeField] private Color slotColor = new Color(0.05f, 0.15f, 0.25f, 0.85f);
        [Tooltip("숫자 키를 보여 줄지 (핫바)")]
        [SerializeField] private bool showKeyNumbers;
        [Tooltip("빈 칸에 쓰는 글자")]
        [SerializeField] private string emptyLabel = "-";

        [Header("줄이 늘 때")]
        [Tooltip("칸 줄이 늘면(가방 확장) 늘어난 높이만큼 아래로 내릴 것 — 핫바 제목 · 핫바 칸 (버그 리포트 10/30 6번: 겹침)")]
        [SerializeField] private RectTransform[] pushDown = new RectTransform[0];
        [Tooltip("칸 줄이 늘면 늘어난 높이만큼 키울 창 패널 (비우면 키우지 않음)")]
        [SerializeField] private RectTransform growPanel;

        private readonly List<HotbarSlotView> slots = new List<HotbarSlotView>();

        /// <summary>칸을 눌렀을 때 (칸 묶음, 칸 번호).</summary>
        public event Action<SlotInventory, int> SlotClicked;

        private SlotInventory Source => inventory == null ? null : part == InventoryPart.Hotbar ? inventory.Hotbar : inventory.Bag;

        private void OnEnable()
        {
            if (inventory == null || slotTemplate == null || slotParent == null)
            {
                Debug.LogError("[SlotGridView] 참조가 비어 있음", this);
                return;
            }
            slotTemplate.gameObject.SetActive(false);
            inventory.Changed += Rebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            if (inventory != null)
                inventory.Changed -= Rebuild;
        }

        private void Rebuild()
        {
            var source = Source;
            if (source == null)
                return;

            while (slots.Count < source.SlotCount)
            {
                int index = slots.Count;
                var slot = Instantiate(slotTemplate, slotParent);
                slot.name = $"Slot{index + 1}";
                slot.Clicked += () => SlotClicked?.Invoke(Source, index);
                // 마우스를 올리면 그 칸의 지금 아이템 설명 — 집기 · 놓기로 바뀌어도 올린 순간의 칸을 본다
                slot.BindTooltip(() => TooltipOf(index));
                slots.Add(slot);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                bool used = i < source.SlotCount;
                slots[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                slots[i].SetContent(showKeyNumbers ? i + 1 : 0, ItemLabel.Of(inventory, source[i], emptyLabel));
                var item = source[i].IsEmpty ? null : inventory.Lookup(source[i].ItemId);
                slots[i].SetIcon(item != null ? item.Icon : null);
                slots[i].SetSelected(false, slotColor, slotColor);
            }
            FitRows(source.SlotCount);
        }

        // 격자(열 고정) 높이를 칸 줄 수에 맞추고, 바뀐 만큼 아래 묶음을 내리고 패널을 키운다 — 줄 수가 같으면 그대로
        private void FitRows(int count)
        {
            if (!(slotParent is RectTransform area) || !slotParent.TryGetComponent<UnityEngine.UI.GridLayoutGroup>(out var grid)
                || grid.constraint != UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount || grid.constraintCount <= 0)
                return;
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)grid.constraintCount));
            float height = rows * grid.cellSize.y + (rows - 1) * grid.spacing.y + grid.padding.vertical;
            float delta = height - area.rect.height;
            if (Mathf.Abs(delta) < 0.5f)
                return;
            area.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            foreach (var rt in pushDown)
                if (rt != null)
                    rt.anchoredPosition -= new Vector2(0f, delta);
            if (growPanel != null)
                growPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, growPanel.rect.height + delta);
        }

        private TooltipContent TooltipOf(int index)
        {
            var source = Source;
            if (source == null || index >= source.SlotCount || source[index].IsEmpty)
                return default;
            return TooltipText.ForItem(inventory.Lookup(source[index].ItemId), source[index].Count);
        }
    }

    /// <summary>칸 글자 규칙 (모든 화면 공통): 쌓이는 아이템은 "이름 ×개수", 도구는 이름만.</summary>
    public static class ItemLabel
    {
        public static string Of(PlayerInventory inventory, ItemStack stack, string emptyLabel)
        {
            var item = inventory.ItemOf(stack);
            if (item == null)
                return emptyLabel;
            return item.MaxStack > 1 ? $"{item.DisplayName} ×{stack.Count}" : item.DisplayName;
        }
    }
}
