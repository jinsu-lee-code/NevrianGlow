using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Interaction;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Farm
{
    /// <summary>
    /// 밭 한 칸의 겉모습 + 상호작용 창구. 상태와 규칙은 FarmGrid(→ Logic FarmRules)에 있고 여기는 전달 · 표시만 한다.
    /// 프리팹 구조: 루트(콜라이더 · Interactable 레이어) / Soil(흙) / Crop(작물) — 모델 교체(5-2)는 자식만 바꾼다.
    /// </summary>
    public class FarmCellView : MonoBehaviour, IItemUseTarget, IHandInteractable, IAimPointTarget
    {
        [Header("참조")]
        [Tooltip("흙 표시 (색으로 개간 · 수분 상태)")]
        [SerializeField] private Renderer soilRenderer;
        [Tooltip("작물 표시 (크기로 성장, 색으로 작물 종류) — 단계 프리팹이 없을 때의 그레이박스")]
        [SerializeField] private Renderer cropRenderer;
        [Tooltip("작물 단계 모델을 놓는 자리 (흙 윗면, 칸 중앙)")]
        [SerializeField] private Transform modelAnchor;

        [Header("조정값")]
        [Tooltip("칸마다 모델을 돌리는 각도 — 같은 작물이 줄지어 있어도 똑같아 보이지 않게 (°)")]
        [SerializeField, Range(0f, 180f)] private float yawStep = 137.5f;

        private FarmGrid grid;
        private int index;
        private MaterialPropertyBlock block;
        private GameObject shownPrefab;
        private GameObject shownModel;
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
        // 보는 방향이 바뀌면 2칸 물뿌리개 짝이 바뀐다 → 프롬프트 칸 수를 다시 받는다
        public bool UpdateAim(Vector3 point, ItemData held) => grid != null && grid.UpdateAim(index, held);
        public void ClearAim() { if (grid != null) grid.ClearAim(); }

        /// <summary>물을 받은 칸의 짧은 반응 — 흙이 살짝 부풀었다 돌아온다 (물방울 VFX는 5-8).</summary>
        public void Splash()
        {
            if (splash != null)
                StopCoroutine(splash);
            splash = StartCoroutine(SplashRoutine());
        }

        private System.Collections.IEnumerator SplashRoutine()
        {
            var soil = soilRenderer.transform;
            if (soilBaseScale == Vector3.zero)
                soilBaseScale = soil.localScale;
            const float duration = 0.25f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t / duration * Mathf.PI); // 0 → 1 → 0
                soil.localScale = new Vector3(soilBaseScale.x * (1f + 0.06f * k), soilBaseScale.y * (1f + 1.5f * k), soilBaseScale.z * (1f + 0.06f * k));
                yield return null;
            }
            soil.localScale = soilBaseScale;
            splash = null;
        }

        private Coroutine splash;
        private Vector3 soilBaseScale;

        /// <summary>
        /// 흙 색과 작물 겉모습을 바꾼다. FarmGrid가 상태가 바뀐 칸에만 부른다.
        /// stagePrefab이 있으면 그 단계 모델(아트), 없으면 그레이박스 상자(색 · 크기 = 성장 비율).
        /// </summary>
        public void Show(Color soilColor, bool hasCrop, Color cropColor, float cropScale, GameObject stagePrefab)
        {
            block.SetColor(BaseColorId, soilColor);
            soilRenderer.SetPropertyBlock(block);

            GameObject wanted = hasCrop && modelAnchor != null ? stagePrefab : null;
            SetModel(wanted);

            bool greybox = hasCrop && wanted == null;
            cropRenderer.gameObject.SetActive(greybox);
            if (!greybox)
                return;
            block.SetColor(BaseColorId, cropColor);
            cropRenderer.SetPropertyBlock(block);
            cropRenderer.transform.localScale = Vector3.one * cropScale;
        }

        // 단계가 바뀔 때만 모델을 바꾼다 (같은 프리팹이면 그대로).
        private void SetModel(GameObject prefab)
        {
            if (prefab == shownPrefab)
                return;
            if (shownModel != null)
                Destroy(shownModel);
            shownPrefab = prefab;
            shownModel = null;
            if (prefab == null)
                return;

            shownModel = Instantiate(prefab, modelAnchor);
            shownModel.transform.localPosition = Vector3.zero;
            shownModel.transform.localRotation = Quaternion.Euler(0f, index * yawStep, 0f);
            // 작물 모델은 보이기만 한다 — 조준(칸 콜라이더)과 플레이어 이동을 막지 않게
            foreach (var c in shownModel.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
        }
    }
}
