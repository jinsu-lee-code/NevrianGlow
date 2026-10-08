using System.Collections.Generic;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;

namespace NevrianGlowFarm.UI
{
    /// <summary>핫바 표시. 도구 수만큼 칸을 만들고, 선택이 바뀔 때만 강조를 옮긴다.</summary>
    public class HotbarView : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("표시할 핫바")]
        [SerializeField] private Hotbar hotbar;
        [Tooltip("칸 원본 — 비활성 자식으로 두면 도구 수만큼 복제한다")]
        [SerializeField] private HotbarSlotView slotTemplate;
        [Tooltip("칸을 늘어놓을 부모 (HorizontalLayoutGroup)")]
        [SerializeField] private Transform slotParent;

        [Header("조정값")]
        [Tooltip("선택되지 않은 칸 색")]
        [SerializeField] private Color normalColor = new Color(0.05f, 0.15f, 0.25f, 0.75f);
        [Tooltip("선택된 칸 색")]
        [SerializeField] private Color selectedColor = new Color(0.38f, 0.95f, 0.88f, 0.9f);

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
            for (int i = 0; i < hotbar.Tools.Count; i++)
            {
                var slot = Instantiate(slotTemplate, slotParent);
                slot.name = $"Slot{i + 1}";
                slot.gameObject.SetActive(true);
                var tool = hotbar.Tools[i];
                slot.SetContent(i + 1, tool != null ? tool.DisplayName : "-");
                slot.SetSelected(false, normalColor, selectedColor);
                slots.Add(slot);
            }

            hotbar.SelectionChanged += Highlight;
            Highlight(hotbar.SelectedIndex);
        }

        private void OnDestroy()
        {
            if (hotbar != null)
                hotbar.SelectionChanged -= Highlight;
        }

        private void Highlight(int index)
        {
            if (highlighted >= 0 && highlighted < slots.Count)
                slots[highlighted].SetSelected(false, normalColor, selectedColor);
            if (index >= 0 && index < slots.Count)
                slots[index].SetSelected(true, normalColor, selectedColor);
            highlighted = index;
        }
    }
}
