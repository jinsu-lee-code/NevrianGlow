using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Interaction;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Farm
{
    /// <summary>
    /// 밭 한 칸의 겉모습 + 상호작용 창구. 상태와 규칙은 FarmGrid(→ Logic FarmRules)에 있고 여기는 전달 · 표시만 한다.
    /// 프리팹 구조: 루트(콜라이더 · Interactable 레이어) / Soil(흙) / Crop(작물) — 모델 교체(5-2)는 자식만 바꾼다.
    /// </summary>
    public class FarmCellView : MonoBehaviour, IItemUseTarget, IHandInteractable, IHandHoldTarget, IAimPointTarget, IAimInfoTarget
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
        [Tooltip("재수확 작물 모델 Animator의 '거둠' 트리거 — 열매 → 봉오리 (아트 ac_crop_lanternfruit_mature)")]
        [SerializeField] private string harvestTrigger = "Harvest";
        [Tooltip("재수확 작물 모델 Animator의 '다시 열림' 트리거 — 봉오리 → 열매")]
        [SerializeField] private string regrowTrigger = "Regrow";
        [Tooltip("재수확 작물 모델 Animator의 '거둔 뒤' 상태 — 막 만든 모델(이어하기 · 날 바뀜)이 열매 없는 나무면 거둠 동작 없이 바로 이 상태로")]
        [SerializeField] private string harvestedState = "IdleHarvested";
        [Tooltip("다 자란 작물의 숨쉬는 빛 높이 — 모델 꼭대기 위 (m, 아트 0.35)")]
        [SerializeField, Min(0f)] private float glowAbove = 0.35f;
        [Tooltip("다 자란 작물 위로 솟는 가는 빛기둥 재질 (URP Unlit 가산 — Build Farm Layout이 만들어 넣음). 비우면 빛기둥 없음 — 시선 유도 1안 (2026-10-25)")]
        [SerializeField] private Material beamMaterial;
        [Tooltip("빛기둥 높이 — 작물 꼭대기부터 (m)")]
        [SerializeField, Min(0f)] private float beamHeight = 2.5f;
        [Tooltip("빛기둥 굵기 (m)")]
        [SerializeField, Min(0.01f)] private float beamWidth = 0.05f;
        [Tooltip("빛기둥 밝기 — 작물 빛 색에 곱한다 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float beamAlpha = 0.45f;
        [Tooltip("비료 즉시 성장 연출 — 작물이 작아졌다 한 단계 커지며 튀어 오르는 시간 (초, 기획서 v2.0: 0.8)")]
        [SerializeField, Min(0f)] private float growBurstTime = 0.8f;
        [Tooltip("비료 즉시 성장 연출의 시작 크기 (배)")]
        [SerializeField, Range(0.1f, 1f)] private float growBurstFrom = 0.55f;

        private FarmGrid grid;
        private int index;
        private MaterialPropertyBlock block;
        private GameObject shownPrefab;
        private GameObject shownModel;
        private Animator fruitAnimator;   // 지금 모델이 열매 트리거를 가진 경우만
        private bool fruitShown;          // 지금 모델에 열매가 보이는지 (새로 만든 완성 모델은 열매가 달려 있음)
        private bool fruitFresh;          // 모델을 막 만들어 아직 열매를 맞춘 적이 없는지
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public int Index => index;
        /// <summary>지금 모델에 수확 창구(나무 · 열매 조준, CropModelTarget)가 있는지.</summary>
        public bool HasModelTarget => modelTarget != null;

        public void Bind(FarmGrid owner, int cellIndex)
        {
            grid = owner;
            index = cellIndex;
            name = $"Cell{cellIndex:00}";
            block ??= new MaterialPropertyBlock();
            soilFx = soilRenderer.GetComponent<SoilFX>();
        }

        private SoilFX soilFx;     // 아트 칸 흙(pf_env_plot_soil)의 괭이질 · 심기 · 물 연출 — 그레이박스 흙이면 없음
        private float shownTill = -1f;
        private bool shownWet;

        /// <summary>
        /// 개간 진행(0~1)을 흙 연출에 알린다. hit = 괭이질 한 번(판 자리가 열리고 흙덩이가 튐), 아니면 연출 없이 그 상태로(불러오기 · 아침 · 수확 뒤).
        /// </summary>
        public void ShowTill(float progress01, bool hit)
        {
            if (soilFx == null)
                return;
            if (hit)
                soilFx.OnTillHit(progress01, soilRenderer.bounds.center + Vector3.up * soilRenderer.bounds.extents.y);
            else if (!Mathf.Approximately(progress01, shownTill))
            {
                if (progress01 <= 0f)
                {
                    soilFx.ResetSoil(); // 젖은 흙도 같이 지워진다
                    shownWet = false;
                }
                else
                    soilFx.SetTill(progress01);
            }
            shownTill = progress01;
        }

        /// <summary>
        /// 물을 준 순간 — 젖은 자국이 칸 위에서 퍼진다 (아트 SoilFX.OnWater, BRANCH_ART 10.28). FarmGrid가 RefreshCell보다 먼저 부른다.
        /// </summary>
        public void Watered()
        {
            if (soilFx == null)
                return;
            soilFx.OnWater(soilRenderer.bounds.center + Vector3.up * soilRenderer.bounds.extents.y);
            shownWet = true;
        }

        /// <summary>젖음 상태를 연출 없이 맞춘다 — 아침에 마름 · 불러오기 (아트 SoilFX.SetWet). 바뀔 때만.</summary>
        public void ShowWet(bool wet)
        {
            if (soilFx == null || wet == shownWet)
                return;
            soilFx.SetWet(wet);
            shownWet = wet;
        }

        /// <summary>심은 순간 — 가운데에 구덩이가 열리고 씨앗이 들어간 뒤 작은 흙무덤 + 씨앗 가루(기획서 v2.0: 0.4초).</summary>
        public void Planted()
        {
            if (soilFx != null)
                soilFx.OnPlant();
            VfxManager.Play(VfxCues.SeedDust, soilRenderer.bounds.center + Vector3.up * (soilRenderer.bounds.extents.y + 0.05f));
        }

        /// <summary>비료로 바로 자란 순간 — 작물이 작아졌다가 한 단계 커지며 튀어 오르고 반짝인다 (기획서 v2.0: 0.8초). FarmGrid가 RefreshCell 뒤에 부른다.</summary>
        public void GrowBurst()
        {
            var target = shownModel != null ? shownModel.transform : cropRenderer.gameObject.activeSelf ? cropRenderer.transform : null;
            if (target == null)
                return;
            if (growing != null)
                StopCoroutine(growing);
            growing = StartCoroutine(GrowRoutine(target, target.localScale));
            VfxManager.Play(VfxCues.Sparkle, (modelAnchor != null ? modelAnchor.position : transform.position) + Vector3.up * 0.2f);
        }

        private Coroutine growing;

        private System.Collections.IEnumerator GrowRoutine(Transform target, Vector3 end)
        {
            for (float t = 0f; t < growBurstTime && target != null; t += Time.deltaTime)
            {
                // 작은 크기에서 살짝 넘쳤다가(1.1배) 제 크기로 — 뒤로 당기는 이징
                float k = t / growBurstTime - 1f;
                float ease = 1f + 2.7f * k * k * k + 1.7f * k * k;
                target.localScale = end * Mathf.LerpUnclamped(growBurstFrom, 1f, ease);
                yield return null;
            }
            if (target != null)
                target.localScale = end;
            growing = null;
        }

        public string GetUsePrompt(ItemData held) => grid != null ? grid.GetUsePrompt(index, held) : null;
        public void Use(ItemData held) { if (grid != null) grid.Use(index, held); }
        public bool HandNeedsHold => true; // 칸의 E는 수확뿐 — E 꾹 (기획서 v2.0)
        public string GetHandPrompt() => grid != null ? grid.GetHandPrompt(index) : null;
        public void InteractHand() { if (grid != null) grid.InteractHand(index); }
        // 보는 방향이 바뀌면 2칸 물뿌리개 짝이 바뀐다 → 프롬프트 칸 수를 다시 받는다
        public bool UpdateAim(Vector3 point, ItemData held) => grid != null && grid.UpdateAim(index, held);
        public void ClearAim() { if (grid != null) grid.ClearAim(); }
        public string GetAimInfo() => grid != null ? grid.GetAimInfo(index) : null;

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
        /// modelTarget = 모델을 조준해야 수확하는 작물(나무형) — 모델의 targetFrom(높이 비율) 위쪽을 감싸는 수확 창구를 붙인다.
        /// </summary>
        public void Show(Color soilColor, bool hasCrop, Color cropColor, float cropScale, GameObject stagePrefab, float modelScale = 1f, bool modelTarget = false, float targetFrom = 0f)
        {
            // 흙 연출(SoilFX)이 같은 블록에 쓴 값은 남기고 색만 바꾼다
            soilRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, soilColor);
            soilRenderer.SetPropertyBlock(block);
            block.Clear();
            cropGlowColor = cropColor;

            GameObject wanted = hasCrop && modelAnchor != null ? stagePrefab : null;
            SetModel(wanted, modelTarget, targetFrom);
            if (shownModel != null)
                shownModel.transform.localScale = Vector3.one * modelScale; // 작물 데이터의 모델 배율 (칸 배율은 부모가 맡는다)
            if (!hasCrop)
                SetGlow(false);

            bool greybox = hasCrop && wanted == null;
            cropRenderer.gameObject.SetActive(greybox);
            if (!greybox)
                return;
            block.SetColor(BaseColorId, cropColor);
            cropRenderer.SetPropertyBlock(block);
            cropRenderer.transform.localScale = Vector3.one * cropScale;
        }

        // 단계가 바뀔 때만 모델을 바꾼다 (같은 프리팹이면 그대로).
        private void SetModel(GameObject prefab, bool withTarget, float targetFrom)
        {
            if (prefab == shownPrefab)
                return;
            if (shownModel != null)
                Destroy(shownModel);
            shownPrefab = prefab;
            shownModel = null;
            modelTarget = null;
            fruitAnimator = null;
            if (prefab == null)
                return;

            shownModel = Instantiate(prefab, modelAnchor);
            shownModel.transform.localPosition = Vector3.zero;
            shownModel.transform.localRotation = Quaternion.Euler(0f, index * yawStep, 0f);
            // 작물 모델은 보이기만 한다 — 조준(칸 콜라이더)과 플레이어 이동을 막지 않게
            foreach (var c in shownModel.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
            if (withTarget)
                AddModelTarget(targetFrom);
            // 열매 트리거(거둠 · 다시 열림)가 둘 다 있는 Animator만 쓴다 — 단계가 바뀔 때 한 번만 찾는다
            var animator = shownModel.GetComponentInChildren<Animator>();
            if (animator != null && HasTrigger(animator, harvestTrigger) && HasTrigger(animator, regrowTrigger))
                fruitAnimator = animator;
            fruitShown = true;
            fruitFresh = true;
        }

        private CropModelTarget modelTarget;

        // 나무형 작물의 수확 창구: 모델의 메시를 감싸는 트리거 상자(이동은 막지 않음, 레이어는 칸과 같은 Interactable).
        // 크기는 만든 순간의 프리팹 자세로 잰다 — 자라기 애니메이션이 아직 돌기 전이라 다 자란 크기.
        // 아래쪽은 모델 높이의 targetFrom 비율부터 — 줄기 메시가 바닥까지 내려와 밑동을 봐도 수확되던 것 (2026-10-30 사용자)
        private void AddModelTarget(float targetFrom)
        {
            var root = shownModel.transform;
            var toRoot = root.worldToLocalMatrix;
            Bounds local = default;
            bool any = false;
            foreach (var r in shownModel.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : r.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                if (mesh == null)
                    continue;
                var m = toRoot * r.transform.localToWorldMatrix;
                var b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { local = new Bounds(corner, Vector3.zero); any = true; }
                    else local.Encapsulate(corner);
                }
            }
            if (!any)
                return;
            float bottom = local.min.y + local.size.y * Mathf.Clamp01(targetFrom);
            local.SetMinMax(new Vector3(local.min.x, bottom, local.min.z), local.max);

            var go = new GameObject("HarvestTarget");
            go.layer = gameObject.layer;
            go.transform.SetParent(root, false);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = local.center;
            box.size = local.size;
            modelTarget = go.AddComponent<CropModelTarget>();
            modelTarget.Bind(grid, index);
        }

        /// <summary>
        /// 재수확 작물의 열매 (FarmGrid가 Show 다음에 부른다). ready = 거둘 수 있음, regrowing = 한 번 이상 거두고 다시 열리는 중.
        /// 열매가 보여야 하는데 숨어 있으면 다시 열림, 반대면 거둠 트리거.
        /// 막 만든 모델(이어하기 · 날 바뀜)이 다시 열리는 중이면 거둠 동작 없이 '거둔 뒤' 상태로 바로 — 열매가 열렸다 떨어지는 모습이 보이던 것 (버그 리포트 10/30 2번)
        /// </summary>
        public void SetFruit(bool ready, bool regrowing)
        {
            SetGlow(ready);
            if (fruitAnimator == null)
                return;
            bool fresh = fruitFresh;
            fruitFresh = false;
            bool wanted = ready || !regrowing;
            if (wanted == fruitShown)
                return;
            int harvestedHash = string.IsNullOrEmpty(harvestedState) ? 0 : Animator.StringToHash(harvestedState);
            if (fresh && !wanted && harvestedHash != 0 && fruitAnimator.HasState(0, harvestedHash))
                fruitAnimator.Play(harvestedHash, 0, 0f);
            else
                fruitAnimator.SetTrigger(wanted ? regrowTrigger : harvestTrigger);
            fruitShown = wanted;
        }

        // 다 자란 작물 위에서 숨쉬는 빛 (VFX glow_pulse, 5-8) — 모델 꼭대기 위 glowAbove m. 그 위로 가는 빛기둥(시선 유도 1안: 멀리서 하늘을 배경으로 보인다)
        private GameObject glow;
        private Transform beam;
        private Renderer beamRenderer;
        private bool glowOn;
        private Color cropGlowColor = Color.white;

        private void SetGlow(bool on)
        {
            if (on == glowOn)
                return;
            glowOn = on;
            float top = ModelTop();
            if (on && glow == null)
                glow = VfxManager.Attach(VfxCues.GlowPulse, transform);
            if (glow != null)
            {
                if (on)
                    glow.transform.position = new Vector3(transform.position.x, top + glowAbove, transform.position.z);
                VfxManager.SetPlaying(glow, on);
            }
            SetBeam(on, top);
        }

        private float ModelTop()
        {
            float top = modelAnchor != null ? modelAnchor.position.y : transform.position.y;
            if (shownModel != null)
                foreach (var r in shownModel.GetComponentsInChildren<Renderer>())
                    if (!(r is ParticleSystemRenderer))
                        top = Mathf.Max(top, r.bounds.max.y);
            return top;
        }

        // 빛기둥: 콜라이더 없는 가는 원기둥(월드 크기 — 칸 배율과 무관), 작물 빛 색 × beamAlpha
        private void SetBeam(bool on, float top)
        {
            if (beamMaterial == null || beamHeight <= 0f)
                return;
            if (beam == null)
            {
                if (!on)
                    return;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "ReadyBeam";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                beam = go.transform;
                beamRenderer = go.GetComponent<Renderer>();
                beamRenderer.sharedMaterial = beamMaterial;
                beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beamRenderer.receiveShadows = false;
            }
            beam.gameObject.SetActive(on);
            if (!on)
                return;
            float bottom = top + glowAbove * 0.5f;
            beam.position = new Vector3(transform.position.x, bottom + beamHeight * 0.5f, transform.position.z);
            beam.rotation = Quaternion.identity;
            var parentScale = transform.lossyScale;
            beam.localScale = new Vector3(beamWidth / Mathf.Max(0.01f, parentScale.x), beamHeight * 0.5f / Mathf.Max(0.01f, parentScale.y), beamWidth / Mathf.Max(0.01f, parentScale.z));
            var color = cropGlowColor;
            color.a = beamAlpha;
            block.Clear();
            block.SetColor(BaseColorId, color);
            beamRenderer.SetPropertyBlock(block);
            block.Clear();
        }

        /// <summary>수확 순간 반짝임 (FarmGrid가 수확 성공 때 부른다).</summary>
        public void HarvestBurst()
        {
            Vector3 p = modelAnchor != null ? modelAnchor.position : transform.position;
            VfxManager.Play(VfxCues.Sparkle, p + Vector3.up * 0.3f);
        }

        private static bool HasTrigger(Animator animator, string name)
        {
            if (string.IsNullOrEmpty(name) || animator.runtimeAnimatorController == null)
                return false;
            foreach (var p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == name)
                    return true;
            return false;
        }
    }
}
