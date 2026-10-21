using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Cleaning;
using NevrianGlowFarm.Systems.Interaction;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Cleaning
{
    /// <summary>
    /// 가게 바닥 얼룩 하나 (GDD 4-4). 양 1 = 처음, 0 = 다 닦임. 대걸레를 들고 좌클릭을 누르고 있으면 MopTool이 줄인다.
    /// 그레이박스: 반투명 원판의 투명도 = 양 — URP Decal 교체는 비주얼(5-x)에서 (모델 자식만 바꾼다).
    /// 조준용 얇은 트리거 콜라이더(Interactable)로 "누르고 있으면 닦기" 프롬프트를 보여 준다.
    /// </summary>
    public class Stain : MonoBehaviour, IItemUseTarget
    {
        [Header("참조")]
        [Tooltip("겉모습 (반투명 원판)")]
        [SerializeField] private Renderer modelRenderer;

        [Header("조정값")]
        [Tooltip("다 더러울 때 색 (알파 = 최대 진하기)")]
        [SerializeField] private Color fullColor = new Color(0.25f, 0.18f, 0.32f, 0.85f);

        [Header("디버그")]
        [Tooltip("남은 양 (0~1, 읽기 전용)")]
        [SerializeField, Range(0f, 1f)] private float amount = 1f;

        private MaterialPropertyBlock block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public float Amount => amount;
        public bool IsClean => amount <= 0f;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            Refresh();
        }

        /// <summary>양을 줄인다. 다 닦이면 사라진다. 실제로 줄인 양.</summary>
        public float Wipe(float wipeAmount)
        {
            float before = amount;
            amount = (float)CleaningRules.Wipe(amount, wipeAmount);
            Refresh();
            if (IsClean)
            {
                gameObject.SetActive(false);
                if (before > 0f)
                    AudioManager.Play(SoundCues.StainClear, transform.position);
            }
            return before - amount;
        }

        public string GetUsePrompt(ItemData held) =>
            held is MopData ? $"누르고 있으면 닦기 (남은 {Mathf.CeilToInt(amount * 100f)}%)" : null;

        // 닦기는 누르고 있는 동안 MopTool이 한다 — 클릭 한 번으로는 아무것도 하지 않는다.
        public void Use(ItemData held) { }

        private void Refresh()
        {
            if (modelRenderer == null || block == null)
                return;
            var c = fullColor;
            c.a *= amount;
            block.SetColor(BaseColorId, c);
            modelRenderer.SetPropertyBlock(block);
        }
    }
}
