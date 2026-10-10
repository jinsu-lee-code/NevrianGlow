using NevrianGlowFarm.Data;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Interaction
{
    /// <summary>
    /// 1-2 상호작용 확인용 대상. E(손 동작)와 좌클릭(든 아이템 사용)을 따로 받아 횟수를 세고, 받을 때마다 색을 바꾼다.
    /// 실제 상호작용 대상(밭 칸 1-4, 문 · 창고 2-x)이 생기면 씬에서 지운다.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class InteractionProbe : MonoBehaviour, IHandInteractable, IItemUseTarget
    {
        [Header("조정값")]
        [Tooltip("상호작용할 때마다 번갈아 칠하는 색")]
        [SerializeField] private Color toggledColor = new Color(0.38f, 0.95f, 0.88f);

        [Header("디버그")]
        [Tooltip("E로 확인한 횟수 (회, 읽기 전용)")]
        [SerializeField] private int handCount;
        [Tooltip("좌클릭으로 아이템을 쓴 횟수 (회, 읽기 전용)")]
        [SerializeField] private int useCount;
        [Tooltip("마지막으로 쓴 아이템 (읽기 전용)")]
        [SerializeField] private string lastUsedItem;

        private Renderer cachedRenderer;
        private MaterialPropertyBlock block;
        private bool toggled;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public int HandCount => handCount;
        public int UseCount => useCount;

        private void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }

        public string GetHandPrompt() => $"손으로 확인 ({handCount}회)";

        public void InteractHand()
        {
            handCount++;
            Toggle();
        }

        // 빈 칸이면 좌클릭으로 할 일이 없다.
        public string GetUsePrompt(ItemData held) => held != null ? $"{held.DisplayName}(으)로 두드리기 ({useCount}회)" : null;

        public void Use(ItemData held)
        {
            useCount++;
            lastUsedItem = held != null ? held.Id : string.Empty;
            Toggle();
        }

        private void Toggle()
        {
            toggled = !toggled;
            if (toggled)
            {
                block.SetColor(BaseColorId, toggledColor);
                cachedRenderer.SetPropertyBlock(block);
            }
            else
            {
                cachedRenderer.SetPropertyBlock(null);
            }
        }
    }
}
