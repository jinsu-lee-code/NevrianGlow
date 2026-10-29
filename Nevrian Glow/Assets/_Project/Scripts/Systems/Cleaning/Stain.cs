using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Cleaning;
using NevrianGlowFarm.Systems.Interaction;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NevrianGlowFarm.Systems.Cleaning
{
    /// <summary>
    /// 가게 바닥 얼룩 하나 (GDD 4-4). 양 1 = 처음, 0 = 다 닦임. 대걸레를 들고 좌클릭을 누르고 있으면 MopTool이 줄인다.
    /// 겉모습: 데칼(아트 얼룩 3종, 진하기 = 양 — 렌더러 Decal 기능 Screen Space) 또는 그레이박스 반투명 원판(투명도 = 양).
    /// 조준용 얇은 트리거 콜라이더(Interactable)로 "누르고 있으면 닦기" 프롬프트를 보여 준다.
    /// </summary>
    public class Stain : MonoBehaviour, IItemUseTarget
    {
        [Header("참조")]
        [Tooltip("겉모습 (반투명 원판)")]
        [SerializeField] private Renderer modelRenderer;
        [Tooltip("얼룩 데칼 (URP Decal Projector) — 있으면 원판 대신 이것을 쓰고 진하기 = 남은 양")]
        [SerializeField] private DecalProjector decal;
        [Tooltip("데칼 재질 후보 — 놓인 자리로 하나 고름 (아트 mat_decal_stain_01 ~ 03)")]
        [SerializeField] private List<Material> decalMaterials = new List<Material>();

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
            if (decal != null)
            {
                if (decalMaterials.Count > 0)
                {
                    int i = Items.ItemModelView.SeedOf(transform.position) % decalMaterials.Count;
                    decal.material = decalMaterials[(i + decalMaterials.Count) % decalMaterials.Count];
                }
                // 얼룩마다 방향을 달리 (같은 자리면 같은 방향)
                decal.transform.localRotation = Quaternion.Euler(90f, (transform.position.x * 53f + transform.position.z * 31f) % 360f, 0f);
                if (modelRenderer != null)
                    modelRenderer.enabled = false;
            }
            Refresh();
        }

        /// <summary>양을 줄인다. 다 닦이면 사라진다. 실제로 줄인 양.</summary>
        public float Wipe(float wipeAmount)
        {
            float before = amount;
            amount = (float)CleaningRules.Wipe(amount, wipeAmount);
            Refresh();
            // 닦는 동안 프롬프트의 "남은 %"가 멈춰 있던 오류 (수정사항 10/28) — 보이는 숫자가 바뀔 때만 다시 받게 한다
            if (Mathf.CeilToInt(before * 100f) != Mathf.CeilToInt(amount * 100f))
                InteractionRaycaster.RequestPromptRefresh();
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
            if (decal != null)
            {
                decal.fadeFactor = amount;
                return;
            }
            if (modelRenderer == null || block == null)
                return;
            var c = fullColor;
            c.a *= amount;
            block.SetColor(BaseColorId, c);
            modelRenderer.SetPropertyBlock(block);
        }
    }
}
