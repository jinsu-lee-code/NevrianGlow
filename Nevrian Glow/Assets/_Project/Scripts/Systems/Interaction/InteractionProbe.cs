using NevrianGlowFarm.Data;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Interaction
{
    /// <summary>
    /// 1-2 상호작용 확인용 대상. E(손 동작)와 좌클릭(든 아이템 사용)을 따로 받아 횟수를 세고,
    /// 받을 때마다 입력별 색으로 잠깐 깜빡인다 (좌클릭 = 시안, E = 마젠타) — 두 입력이 서로의 표시를 지우지 않는다.
    /// 실제 상호작용 대상(밭 칸 1-4, 문 · 창고 2-x)이 생기면 씬에서 지운다.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class InteractionProbe : MonoBehaviour, IHandInteractable, IItemUseTarget
    {
        [Header("조정값")]
        [Tooltip("좌클릭(아이템 사용)을 받았을 때 깜빡이는 색")]
        [SerializeField] private Color useColor = new Color(0.38f, 0.95f, 0.88f);
        [Tooltip("E(손 동작)를 받았을 때 깜빡이는 색")]
        [SerializeField] private Color handColor = new Color(1f, 0.45f, 0.85f);
        [Tooltip("깜빡임이 유지되는 시간 (초)")]
        [SerializeField, Min(0.05f)] private float flashDuration = 0.25f;

        [Header("디버그")]
        [Tooltip("E로 확인한 횟수 (회, 읽기 전용)")]
        [SerializeField] private int handCount;
        [Tooltip("좌클릭으로 아이템을 쓴 횟수 (회, 읽기 전용)")]
        [SerializeField] private int useCount;
        [Tooltip("마지막으로 쓴 아이템 (읽기 전용)")]
        [SerializeField] private string lastUsedItem;

        private Renderer cachedRenderer;
        private MaterialPropertyBlock block;
        private float flashEndTime;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public int HandCount => handCount;
        public int UseCount => useCount;

        private void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            enabled = false; // 깜빡이는 동안에만 Update
        }

        public string GetHandPrompt() => $"손으로 확인 ({handCount}회)";

        public void InteractHand()
        {
            handCount++;
            Flash(handColor);
        }

        // 빈 칸이면 좌클릭으로 할 일이 없다.
        public string GetUsePrompt(ItemData held) => held != null ? $"{held.DisplayName}(으)로 두드리기 ({useCount}회)" : null;

        public void Use(ItemData held)
        {
            useCount++;
            lastUsedItem = held != null ? held.Id : string.Empty;
            Flash(useColor);
        }

        // 새 입력이 오면 이전 깜빡임을 덮어쓰고 시간을 다시 잰다.
        private void Flash(Color color)
        {
            block.SetColor(BaseColorId, color);
            cachedRenderer.SetPropertyBlock(block);
            flashEndTime = Time.time + flashDuration;
            enabled = true;
        }

        private void Update()
        {
            if (Time.time < flashEndTime)
                return;
            cachedRenderer.SetPropertyBlock(null);
            enabled = false;
        }
    }
}
