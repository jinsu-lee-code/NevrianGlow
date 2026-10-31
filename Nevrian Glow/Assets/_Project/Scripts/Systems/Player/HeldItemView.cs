using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Cleaning;
using NevrianGlowFarm.Systems.Interaction;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Player
{
    /// <summary>
    /// 1인칭 손에 든 아이템 (CLAUDE 핵심 규칙 4: 팔 리그 없음, 도구 동작은 코드 애니메이션). 1인칭 카메라의 자식.
    /// 고른 핫바 칸의 아이템을 화면 오른쪽 아래에 보여 주고, 쓸 때 종류별로 움직인다:
    /// 괭이 = 내려치기, 물뿌리개 = 기울이기(도구 UseDelay 동안 — 0.3초 뒤 물), 대걸레 = 누르는 동안 앞뒤로 밀기, 그 외 = 뿌리기.
    /// 모델은 ItemData.HeldPrefab, 비어 있으면 종류별 그레이박스 도형.
    /// </summary>
    public class HeldItemView : MonoBehaviour
    {
        private enum Motion
        {
            Swing,
            Tilt,
            Push,
            Toss,
        }

        [Header("참조")]
        [Tooltip("든 아이템을 알려 주는 핫바")]
        [SerializeField] private Hotbar hotbar;
        [Tooltip("좌클릭 사용을 알려 주는 상호작용 (ItemUsed)")]
        [SerializeField] private InteractionRaycaster raycaster;
        [Tooltip("대걸레질 중인지 알려 주는 도구")]
        [SerializeField] private MopTool mopTool;
        [Tooltip("그레이박스 도형 재질 (색은 칸마다 바꾼다)")]
        [SerializeField] private Material greyboxMaterial;

        [Header("조정값")]
        [Tooltip("들고 있는 자세 위치 — 카메라 기준 (m)")]
        [SerializeField] private Vector3 holdPosition = new Vector3(0.36f, -0.34f, 0.8f);
        [Tooltip("들고 있는 자세 회전 — 카메라 기준 (°)")]
        [SerializeField] private Vector3 holdRotation = new Vector3(10f, -12f, 0f);
        [Tooltip("바꿔 들 때 아래에서 올라오는 시간 (초)")]
        [SerializeField, Min(0.01f)] private float equipTime = 0.15f;
        [Tooltip("내려치기 시간 (초)")]
        [SerializeField, Min(0.05f)] private float swingTime = 0.3f;
        [Tooltip("물뿌리개 기울기 — 도구 UseDelay 동안 기울고 잠깐 멈췄다 돌아온다 (°)")]
        [SerializeField, Range(0f, 90f)] private float tiltAngle = 55f;
        [Tooltip("뿌리기 시간 (초)")]
        [SerializeField, Min(0.05f)] private float tossTime = 0.25f;
        [Tooltip("대걸레 밀기 거리 · 빠르기 — 모델에 Contact 표시점이 없을 때만 (m · 회/초)")]
        [SerializeField] private Vector2 pushStrokeAndRate = new Vector2(0.09f, 3f);
        [Tooltip("대걸레질 좌우 폭 — 머리가 얼룩 위를 오가는 거리의 절반 (m)")]
        [SerializeField, Range(0f, 0.6f)] private float scrubWidth = 0.22f;
        [Tooltip("대걸레질 빠르기 (왕복/초)")]
        [SerializeField, Range(0.2f, 4f)] private float scrubRate = 1.4f;
        [Tooltip("대걸레질 중 좌우로 기우는 각도 (°)")]
        [SerializeField, Range(0f, 30f)] private float scrubLean = 10f;
        [Tooltip("대걸레질 중 대걸레 크기 배율 — 클수록 머리가 바닥 가까이 닿지만 막대가 굵어 보인다 (배)")]
        [SerializeField, Range(1f, 3f)] private float scrubSize = 1.8f;
        [Tooltip("대걸레질 중 손을 내리는 거리 — 바닥을 미는 자세 (m)")]
        [SerializeField, Range(0f, 0.5f)] private float scrubHandDrop = 0.3f;
        [Tooltip("들고 있는 자세 ↔ 바닥 대걸레질 자세 전환 시간 (초)")]
        [SerializeField, Min(0.01f)] private float scrubBlendTime = 0.12f;
        [Tooltip("손에 든 발광 도구의 빛 세기 — 아트 재질 값에 곱한다. 화면 바로 앞이라 그대로면 눈부심 (버그 리포트 10/30 3번) (배)")]
        [SerializeField, Range(0f, 1f)] private float heldGlowScale = 0.35f;
        [Tooltip("손 모델이 따로 없는 아이템(수확물 등)을 놓인 모습 모델로 들 때 맞추는 크기 — 가장 긴 변 (m)")]
        [SerializeField, Min(0.01f)] private float worldModelHeldSize = 0.16f;

        [Header("디버그")]
        [Tooltip("지금 동작 (읽기 전용)")]
        [SerializeField] private string currentMotion;

        private Transform pivot;
        private GameObject model;
        private ItemData shown;
        private Motion motion;
        private float equipStart = -10f;
        private float useStart = -10f;
        private float useLength;
        private float tiltHold;
        private Transform contact;
        private Transform grip;
        private float scrubBlend;
        private float scrubPhase;
        private Vector3 scrubPoint;
        private MaterialPropertyBlock block;
        private const string ContactName = "Contact";
        private const string GripName = "Grip";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            if (hotbar == null || raycaster == null || greyboxMaterial == null)
            {
                Debug.LogError("[HeldItemView] 참조가 비어 있음 - 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            block = new MaterialPropertyBlock();
            pivot = new GameObject("HeldPivot").transform;
            pivot.SetParent(transform, false);
        }

        private void OnEnable()
        {
            if (hotbar != null) { hotbar.SelectionChanged += OnSelection; hotbar.SlotsChanged += OnSlots; }
            if (raycaster != null) raycaster.ItemUsed += OnUsed;
        }

        private void OnDisable()
        {
            if (hotbar != null) { hotbar.SelectionChanged -= OnSelection; hotbar.SlotsChanged -= OnSlots; }
            if (raycaster != null) raycaster.ItemUsed -= OnUsed;
        }

        private void Start() => Show(hotbar.SelectedItem);

        private void OnSelection(int index) => Show(hotbar.SelectedItem);

        // 고른 칸이 그대로여도 내용이 바뀔 수 있다(씨앗을 다 씀 · 창고에서 꺼냄)
        private void OnSlots()
        {
            if (hotbar.SelectedItem != shown)
                Show(hotbar.SelectedItem);
        }

        private void OnUsed(ItemData item) => PlayUse(item);

        /// <summary>든 아이템의 사용 동작을 한 번 재생한다 (동작 중이면 무시). 보통은 InteractionRaycaster.ItemUsed가 부른다.</summary>
        public void PlayUse(ItemData item)
        {
            if (item == null || item != shown || Time.time < useStart + useLength)
                return;
            useStart = Time.time;
            switch (motion)
            {
                case Motion.Swing: useLength = swingTime; break;
                case Motion.Tilt:
                    tiltHold = item is ToolData tool && tool.UseDelay > 0f ? tool.UseDelay : 0.3f;
                    useLength = tiltHold + 0.35f;
                    break;
                case Motion.Toss: useLength = tossTime; break;
                default: useLength = 0f; break;
            }
        }

        private void Update()
        {
            if (model == null)
                return;
            // 화면(가방 · 창고)이 열리거나 장소 이동 중에는 숨긴다
            bool visible = !PlayerInputGate.IsBlocked;
            if (model.activeSelf != visible)
                model.SetActive(visible);
            if (!visible)
                return;

            UpdateVfx();
            Vector3 pos = holdPosition;
            Vector3 rot = holdRotation;

            float equip = Mathf.Clamp01((Time.time - equipStart) / equipTime);
            pos += Vector3.down * (1f - Smooth(equip)) * 0.25f;

            float t = useLength > 0f ? (Time.time - useStart) / useLength : 1f;
            bool mopping = motion == Motion.Push && mopTool != null && mopTool.IsMopping;
            currentMotion = t < 1f ? motion.ToString() : mopping ? (contact != null ? "Scrub" : "Push") : "-";
            if (t < 1f)
                Animate(ref pos, ref rot, Mathf.Clamp01(t));
            else if (mopping && contact == null)
                pos += Vector3.forward * Mathf.Sin(Time.time * pushStrokeAndRate.y * Mathf.PI * 2f) * pushStrokeAndRate.x;

            var rotation = Quaternion.Euler(rot);
            Scrub(mopping, ref pos, ref rotation);
            pivot.localPosition = pos;
            pivot.localRotation = rotation;
        }

        /// <summary>
        /// 대걸레질: 손잡이(Grip)는 손 자리에 둔 채 대걸레를 기울이고 손에서 바닥까지 닿는 길이로 키워,
        /// 머리(Contact)가 닦는 바닥 지점(MopTool — 닿는 거리 안) 위를 좌우로 훔친다. 들고 있는 자세와 섞어(scrubBlendTime) 바꾼다.
        /// </summary>
        private void Scrub(bool mopping, ref Vector3 pos, ref Quaternion rotation)
        {
            if (contact == null)
            {
                scrubBlend = 0f;
                pivot.localScale = Vector3.one;
                return;
            }
            if (mopping && mopTool.HasPoint)
                scrubPoint = mopTool.Point;
            scrubBlend = Mathf.MoveTowards(scrubBlend, mopping ? 1f : 0f, Time.deltaTime / scrubBlendTime);
            if (scrubBlend <= 0f)
            {
                scrubPhase = 0f;
                pivot.localScale = Vector3.one;
                return;
            }
            scrubPhase += Time.deltaTime * scrubRate * Mathf.PI * 2f;
            float sweep = Mathf.Sin(scrubPhase);

            // 들고 있는 자세에서의 손잡이 · 머리 (피벗 기준 — 피벗 크기 1일 때 값)
            Vector3 gripLocal = pivot.InverseTransformPoint(grip != null ? grip.position : model.transform.position);
            Vector3 contactLocal = pivot.InverseTransformPoint(contact.position);
            // 손은 들고 있는 자리보다 조금 아래 · 앞으로 (바닥을 미는 자세), 훔치는 쪽으로 조금 따라감
            Vector3 gripCam = pos + rotation * gripLocal + new Vector3(sweep * scrubWidth * 0.2f, -scrubHandDrop, scrubHandDrop * 0.5f);

            // 머리 목표: 닦는 바닥 지점을 좌우로 오가는 점. 머리는 카메라에서 그 점으로 가는 시선 위,
            // 손잡이에서 (키운) 대걸레 길이만큼 떨어진 곳 → 화면에서 머리가 얼룩과 겹쳐 보인다
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 dir = transform.InverseTransformPoint(scrubPoint + right * sweep * scrubWidth);
            float floorDistance = dir.magnitude;
            dir /= Mathf.Max(0.001f, floorDistance);
            Vector3 restShaft = rotation * (contactLocal - gripLocal);
            float size = scrubSize;
            float length = restShaft.magnitude * size;
            float b = Vector3.Dot(dir, gripCam);
            float disc = b * b - gripCam.sqrMagnitude + length * length;
            float along = disc > 0f ? b + Mathf.Sqrt(disc) : Mathf.Max(0.1f, b);
            Vector3 headCam = dir * Mathf.Min(along, floorDistance); // 바닥보다 멀리 가지 않게
            Vector3 shaft = headCam - gripCam;

            // 손잡이를 축으로 대걸레를 돌려 머리를 목표로 — 막대 축 둘레는 모델의 위(+Y)가 실제 위를 보게 맞춰
            // 머리 밑면이 바닥을 누르는 모양으로, 그다음 훔치는 쪽으로 조금 비튼다
            Vector3 modelUp = rotation * pivot.InverseTransformDirection(contact.parent != null ? contact.parent.up : pivot.up);
            Vector3 worldUp = transform.InverseTransformDirection(Vector3.up);
            Vector3 restUp = Vector3.ProjectOnPlane(modelUp, restShaft);
            Vector3 wantUp = Vector3.ProjectOnPlane(worldUp, shaft);
            Quaternion align = restUp.sqrMagnitude > 1e-6f && wantUp.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(shaft, wantUp) * Quaternion.Inverse(Quaternion.LookRotation(restShaft, restUp))
                : Quaternion.FromToRotation(restShaft, shaft);
            var scrubRotation = align * rotation;
            scrubRotation = Quaternion.AngleAxis(-sweep * scrubLean, shaft.normalized) * scrubRotation;
            Vector3 scrubPos = gripCam - scrubRotation * (gripLocal * size);

            float w = Mathf.SmoothStep(0f, 1f, scrubBlend);
            pos = Vector3.Lerp(pos, scrubPos, w);
            rotation = Quaternion.Slerp(rotation, scrubRotation, w);
            pivot.localScale = Vector3.one * Mathf.Lerp(1f, size, w);
        }

        private void Animate(ref Vector3 pos, ref Vector3 rot, float t)
        {
            switch (motion)
            {
                case Motion.Swing:
                    // 0 ~ 0.35 들어 올림 → 0.35 ~ 0.6 내려침 → 돌아옴
                    float swing = t < 0.35f ? -35f * Smooth(t / 0.35f)
                        : t < 0.6f ? Mathf.Lerp(-35f, 60f, Smooth((t - 0.35f) / 0.25f))
                        : Mathf.Lerp(60f, 0f, Smooth((t - 0.6f) / 0.4f));
                    rot.x += swing;
                    pos += Vector3.down * Mathf.Max(0f, swing) * 0.002f;
                    break;
                case Motion.Tilt:
                    float elapsed = t * useLength;
                    float tilt = elapsed < tiltHold ? Smooth(elapsed / tiltHold)
                        : elapsed < tiltHold + 0.1f ? 1f
                        : 1f - Smooth((elapsed - tiltHold - 0.1f) / 0.25f);
                    rot.x += tiltAngle * tilt; // 앞으로 기울여 붓는다
                    break;
                case Motion.Toss:
                    float toss = Mathf.Sin(t * Mathf.PI);
                    rot.x += 25f * toss;
                    pos += Vector3.forward * 0.08f * toss;
                    break;
            }
        }

        private static float Smooth(float x) => x * x * (3f - 2f * x);

        // ── VFX (5-8): 물뿌리개 물줄기 · 대걸레 거품 ──

        private GameObject stream;   // 물뿌리개 주둥이 아래 (모델이 바뀌면 함께 지워짐)
        private GameObject foam;     // 월드 — 닦는 바닥 지점
        private bool streamOn, foamOn;

        private void UpdateVfx()
        {
            // 물줄기: 기울여 붓는 동안 (기울기 60% ~ 멈춤 끝)
            bool pour = false;
            if (stream != null && motion == Motion.Tilt && useLength > 0f)
            {
                float elapsed = Time.time - useStart;
                pour = elapsed >= tiltHold * 0.6f && elapsed < tiltHold + 0.1f;
            }
            if (pour != streamOn)
            {
                streamOn = pour;
                VfxManager.SetPlaying(stream, pour);
            }

            // 거품: 대걸레질 중 닦는 지점에 (한 덩어리씩 — 아트 bubble_foam)
            bool scrub = motion == Motion.Push && mopTool != null && mopTool.IsMopping && mopTool.HasPoint;
            if (scrub && foam == null)
                foam = VfxManager.Attach(VfxCues.BubbleFoam, null);
            if (foam != null)
            {
                if (scrub)
                    foam.transform.position = mopTool.Point;
                if (scrub != foamOn)
                {
                    foamOn = scrub;
                    VfxManager.SetPlaying(foam, scrub);
                }
            }
        }

        // 물뿌리개 모델 앞쪽 위(주둥이)에 물줄기 — 루트 +Z가 주둥이 방향 (모델 +Z = 앞)
        private void AttachStream()
        {
            streamOn = false;
            if (!(shown is WateringCanData) || model == null)
                return;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;
            var b = renderers[0].bounds;
            foreach (var r in renderers)
                b.Encapsulate(r.bounds);
            var anchor = new GameObject("Spout").transform;
            anchor.SetParent(model.transform, false);
            // 월드 경계 → 모델 로컬: 앞(+Z) 끝, 위쪽 70% 높이
            Vector3 front = model.transform.InverseTransformPoint(b.center + model.transform.forward * Vector3.Dot(b.extents, Abs(model.transform.forward)));
            Vector3 top = model.transform.InverseTransformPoint(b.center + Vector3.up * b.extents.y * 0.4f);
            anchor.localPosition = new Vector3(front.x, top.y, front.z);
            anchor.localRotation = Quaternion.identity;
            stream = VfxManager.Attach(VfxCues.WaterStream, anchor);
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private void OnDestroy()
        {
            if (foam != null)
                Destroy(foam);
        }

        // ── 모델 ───────────────────────────────────────────

        private void Show(ItemData item)
        {
            if (item == shown && model != null)
                return;
            shown = item;
            if (foamOn)
            {
                foamOn = false;
                VfxManager.SetPlaying(foam, false);
            }
            if (model != null)
                Destroy(model);
            model = null;
            useLength = 0f;
            if (item == null)
                return;

            motion = item is HoeData ? Motion.Swing : item is WateringCanData ? Motion.Tilt : item is MopData ? Motion.Push : Motion.Toss;
            // 손 모델 → 없으면 놓인 모습 모델(수확물 — 임시 구체가 보이던 것, 버그 리포트 10/30 8번) → 없으면 그레이박스
            var worldModel = item.HeldPrefab == null ? item.WorldModelFor(0) : null;
            if (item.HeldPrefab != null)
                model = Instantiate(item.HeldPrefab, pivot, false);
            else if (worldModel != null)
            {
                model = Instantiate(worldModel, pivot, false);
                model.transform.localScale = Vector3.one * (worldModelHeldSize / Items.ItemModelView.LongestSide(worldModel));
            }
            else
                model = BuildGreybox(item);
            Rendering.GlowDimmer.Apply(model, heldGlowScale); // 손에 든 발광 도구는 화면 바로 앞이라 눈부심 (버그 리포트 10/30 3번)
            contact = null;
            grip = null;
            scrubBlend = 0f;
            // 쓰는 동작용 표시점 (빈 오브젝트): Contact = 대걸레 머리 밑, Grip = 손잡이 (없으면 모델 원점)
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == ContactName)
                    contact = t;
                else if (t.name == GripName)
                    grip = t;
            }
            foreach (var c in model.GetComponentsInChildren<Collider>())
                Destroy(c);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            AttachStream();
            equipStart = Time.time;
        }

        private GameObject BuildGreybox(ItemData item)
        {
            var root = new GameObject($"Held_{item.Id}");
            root.transform.SetParent(pivot, false);
            var wood = new Color(0.45f, 0.33f, 0.22f);
            var metal = new Color(0.62f, 0.66f, 0.72f);
            switch (item)
            {
                case HoeData _:
                    Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.28f, 0.03f), Vector3.zero, wood);
                    Part(root, PrimitiveType.Cube, new Vector3(0f, 0.27f, 0.05f), new Vector3(0.14f, 0.035f, 0.12f), Vector3.zero, metal);
                    break;
                case WateringCanData _:
                    Part(root, PrimitiveType.Cube, Vector3.zero, new Vector3(0.16f, 0.14f, 0.2f), Vector3.zero, new Color(0.3f, 0.55f, 0.75f));
                    Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0.16f), new Vector3(0.03f, 0.08f, 0.03f), new Vector3(60f, 0f, 0f), new Color(0.3f, 0.55f, 0.75f));
                    break;
                case MopData _:
                    // 막대를 앞으로 눕혀 걸레 머리가 화면 안에 들어오게
                    Part(root, PrimitiveType.Cylinder, new Vector3(-0.04f, 0.1f, -0.05f), new Vector3(0.025f, 0.3f, 0.025f), new Vector3(-45f, 0f, 0f), wood);
                    Part(root, PrimitiveType.Cube, new Vector3(-0.04f, -0.12f, 0.17f), new Vector3(0.22f, 0.05f, 0.1f), Vector3.zero, new Color(0.85f, 0.85f, 0.8f));
                    break;
                case CropData crop:
                    Part(root, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.12f, Vector3.zero, crop.GreyboxColor);
                    break;
                case SeedData seed:
                    Part(root, PrimitiveType.Cube, Vector3.zero, new Vector3(0.12f, 0.14f, 0.06f), Vector3.zero, new Color(0.55f, 0.45f, 0.3f));
                    if (seed.Crop != null)
                        Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.09f, 0f), Vector3.one * 0.05f, Vector3.zero, seed.Crop.GreyboxColor);
                    break;
                default:
                    Part(root, PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.1f, Vector3.zero, new Color(0.5f, 0.5f, 0.55f));
                    break;
            }
            return root;
        }

        private void Part(GameObject root, PrimitiveType type, Vector3 localPos, Vector3 scale, Vector3 euler, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = greyboxMaterial;
            block.SetColor(BaseColorId, color);
            r.SetPropertyBlock(block);
        }
    }
}
