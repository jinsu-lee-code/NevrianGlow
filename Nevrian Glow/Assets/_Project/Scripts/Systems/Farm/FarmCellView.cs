using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Interaction;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Farm
{
    /// <summary>
    /// 밭 한 칸의 겉모습 + 상호작용 창구. 상태와 규칙은 FarmGrid(→ Logic FarmRules)에 있고 여기는 전달 · 표시만 한다.
    /// 프리팹 구조: 루트(콜라이더 · Interactable 레이어) / Soil(흙) / Crop(작물) — 모델 교체(5-2)는 자식만 바꾼다.
    /// </summary>
    public class FarmCellView : MonoBehaviour, IItemUseTarget, IHandInteractable
    {
        [Header("참조")]
        [Tooltip("흙 표시 (색으로 개간 · 수분 상태)")]
        [SerializeField] private Renderer soilRenderer;
        [Tooltip("작물 표시 (크기로 성장, 색으로 작물 종류)")]
        [SerializeField] private Renderer cropRenderer;

        private FarmGrid grid;
        private int index;
        private MaterialPropertyBlock block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public int Index => index;

        public void Bind(FarmGrid owner, int cellIndex)
        {
            grid = owner;
            index = cellIndex;
            name = $"Cell{cellIndex:00}";
            block ??= new MaterialPropertyBlock();
        }

        public string GetUsePrompt(ItemData held) => grid != null ? grid.GetUsePrompt(index, held) : null;
        public void Use(ItemData held) { if (grid != null) grid.Use(index, held); }
        public string GetHandPrompt() => grid != null ? grid.GetHandPrompt(index) : null;
        public void InteractHand() { if (grid != null) grid.InteractHand(index); }

        /// <summary>흙 색, 작물 표시 여부 · 색 · 크기(0~1 성장 비율)를 바꾼다. FarmGrid가 상태가 바뀐 칸에만 부른다.</summary>
        public void Show(Color soilColor, bool hasCrop, Color cropColor, float cropScale)
        {
            block.SetColor(BaseColorId, soilColor);
            soilRenderer.SetPropertyBlock(block);

            cropRenderer.gameObject.SetActive(hasCrop);
            if (!hasCrop)
                return;
            block.SetColor(BaseColorId, cropColor);
            cropRenderer.SetPropertyBlock(block);
            cropRenderer.transform.localScale = Vector3.one * cropScale;
        }
    }
}
