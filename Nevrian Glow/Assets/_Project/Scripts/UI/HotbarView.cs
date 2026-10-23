using System.Collections.Generic;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;

namespace NevrianGlowFarm.UI
{
    /// <summary>핫바 표시. 칸 수만큼 칸을 만들고, 칸이 바뀔 때만 다시 그리고, 선택이 바뀔 때만 강조를 옮긴다.</summary>
    public class HotbarView : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("표시할 핫바")]
        [SerializeField] private Hotbar hotbar;
        [Tooltip("칸 원본 — 비활성 자식으로 두면 칸 수만큼 복제한다")]
        [SerializeField] private HotbarSlotView slotTemplate;
        [Tooltip("칸을 늘어놓을 부모 (HorizontalLayoutGroup)")]
        [SerializeField] private Transform slotParent;

        [Header("조정값")]
        [Tooltip("선택되지 않은 칸 색")]
        [SerializeField] private Color normalColor = new Color(0.05f, 0.15f, 0.25f, 0.75f);
        [Tooltip("선택된 칸 색")]
        [SerializeField] private Color selectedColor = new Color(0.38f, 0.95f, 0.88f, 0.9f);
        [Tooltip("빈 칸에 쓰는 글자")]
        [SerializeField] private string emptyLabel = "-";

        private readonly List<HotbarSlotView> slots = new List<HotbarSlotView>();
        private int highlighted = -1;

        private void Start()
        {
            if (hotbar == null || slotTemplate == null || slotParent == null)
            {
                Debug.LogError("[HotbarView] 참조가 비어 있음", this);
                return;
            }

            slotTemplate.gameObject.SetActive(false);
            Rebuild();
            hotbar.SlotsChanged += Rebuild;
            hotbar.SelectionChanged += Highlight;
        }

        private void OnDestroy()
        {
            if (hotbar == null)
                return;
            hotbar.SlotsChanged -= Rebuild;
            hotbar.SelectionChanged -= Highlight;
        }

        private void Rebuild()
        {
            // 칸 수가 바뀌면 모자란 만큼 만들고 남는 만큼 숨긴다 (만든 칸은 재사용).
            while (slots.Count < hotbar.SlotCount)
            {
                var slot = Instantiate(slotTemplate, slotParent);
                slot.name = $"Slot{slots.Count + 1}";
                slots.Add(slot);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                bool used = i < hotbar.SlotCount;
                slots[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                var item = hotbar.GetItem(i);
                int count = hotbar.GetCount(i);
                string label = item == null ? emptyLabel : item.MaxStack > 1 ? $"{item.DisplayName} ×{count}" : item.DisplayName;
                slots[i].SetContent(i + 1, label);
                slots[i].SetIcon(item != null ? item.Icon : null);
                slots[i].SetSelected(false, normalColor, selectedColor);
            }

            highlighted = -1;
            Highlight(hotbar.SelectedIndex);
        }

        private void Highlight(int index)
        {
            if (highlighted >= 0 && highlighted < slots.Count)
                slots[highlighted].SetSelected(false, normalColor, selectedColor);
            if (index >= 0 && index < hotbar.SlotCount)
                slots[index].SetSelected(true, normalColor, selectedColor);
            highlighted = index;
        }
    }
}
