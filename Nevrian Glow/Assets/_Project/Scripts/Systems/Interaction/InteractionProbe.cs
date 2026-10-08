using NevrianGlowFarm.Data;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Interaction
{
    /// <summary>
    /// 1-2 상호작용 확인용 대상. 도구 이름으로 프롬프트를 만들고, 상호작용할 때마다 색을 바꾼다.
    /// 실제 상호작용 대상(밭 칸 1-4, 문 · 창고 2-x)이 생기면 씬에서 지운다.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class InteractionProbe : MonoBehaviour, IInteractable
    {
        [Header("조정값")]
        [Tooltip("상호작용할 때마다 번갈아 칠하는 색")]
        [SerializeField] private Color toggledColor = new Color(0.38f, 0.95f, 0.88f);

        [Header("디버그")]
        [Tooltip("지금까지 상호작용한 횟수 (회, 읽기 전용)")]
        [SerializeField] private int interactCount;

        private Renderer cachedRenderer;
        private MaterialPropertyBlock block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public int InteractCount => interactCount;

        private void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }

        public string GetPrompt(ToolData tool)
        {
            string toolName = tool != null ? tool.DisplayName : "빈 손";
            return $"[E / 클릭] {toolName}(으)로 확인 ({interactCount}회)";
        }

        public void Interact(ToolData tool)
        {
            interactCount++;
            if (interactCount % 2 == 1)
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
