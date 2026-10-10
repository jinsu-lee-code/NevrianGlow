using System.Collections.Generic;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Interaction;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Items
{
    /// <summary>
    /// 바닥에 내려놓은 아이템 한 묶음 (GDD 4-3). 사라지지 않고 날짜가 지나도 그 자리에 남는다(저장 6-1 대상: id · 개수 · 구역 · 위치 · 회전).
    /// E로 줍는다 — 넣을 칸이 없으면 줍지 못하고 안내. 놓인 구역 씬(Farm · Shop)에 속한다.
    /// 프리팹 구조: 루트(콜라이더 · Interactable 레이어) / Model(겉모습) — 모델 교체(5-2)는 자식만.
    /// </summary>
    public class WorldItem : MonoBehaviour, IHandInteractable
    {
        [Header("참조")]
        [Tooltip("겉모습 (그레이박스: 아이템 색의 작은 도형)")]
        [SerializeField] private Renderer modelRenderer;

        [Header("조정값")]
        [Tooltip("작물이 아닌 아이템의 그레이박스 색")]
        [SerializeField] private Color defaultColor = new Color(0.6f, 0.62f, 0.66f);

        [Header("디버그")]
        [Tooltip("아이템 id (읽기 전용)")]
        [SerializeField] private string itemId;
        [Tooltip("개수 (읽기 전용)")]
        [SerializeField] private int count;

        private ItemData item;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly List<WorldItem> all = new List<WorldItem>();

        /// <summary>지금 놓여 있는 모든 바닥 아이템 (청결도 · 배치 · 저장 6-1이 쓴다 — 매 프레임 찾지 않게).</summary>
        public static IReadOnlyList<WorldItem> All => all;

        public ItemData Item => item;
        public string ItemId => itemId;
        public int Count => count;

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        /// <summary>내용을 정한다 (내려놓을 때 · 불러올 때).</summary>
        public void Init(ItemData data, int amount)
        {
            item = data;
            itemId = data != null ? data.Id : string.Empty;
            count = amount;
            name = $"WorldItem_{itemId}";

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, data is CropData crop ? crop.GreyboxColor : defaultColor);
            modelRenderer.SetPropertyBlock(block);
        }

        public string GetHandPrompt()
        {
            if (item == null)
                return null;
            var inventory = PlayerInventory.Instance;
            string label = count > 1 ? $"{item.DisplayName} ×{count}" : item.DisplayName;
            return inventory != null && inventory.CanAdd(item, count)
                ? $"{label} 줍기"
                : $"{label} 줍기 불가 - 넣을 칸 없음";
        }

        public void InteractHand()
        {
            var inventory = PlayerInventory.Instance;
            if (item == null || inventory == null || !inventory.TryAdd(item, count))
                return;
            Destroy(gameObject);
        }
    }
}
