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
        [Tooltip("대걸레 밀기 거리 · 빠르기 (m · 회/초)")]
        [SerializeField] private Vector2 pushStrokeAndRate = new Vector2(0.09f, 3f);

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
        private MaterialPropertyBlock block;
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

            Vector3 pos = holdPosition;
            Vector3 rot = holdRotation;

            float equip = Mathf.Clamp01((Time.time - equipStart) / equipTime);
            pos += Vector3.down * (1f - Smooth(equip)) * 0.25f;

            float t = useLength > 0f ? (Time.time - useStart) / useLength : 1f;
            currentMotion = t < 1f ? motion.ToString() : (motion == Motion.Push && mopTool != null && mopTool.IsMopping ? "Push" : "-");
            if (t < 1f)
                Animate(ref pos, ref rot, Mathf.Clamp01(t));
            else if (motion == Motion.Push && mopTool != null && mopTool.IsMopping)
                pos += Vector3.forward * Mathf.Sin(Time.time * pushStrokeAndRate.y * Mathf.PI * 2f) * pushStrokeAndRate.x;

            pivot.localPosition = pos;
            pivot.localRotation = Quaternion.Euler(rot);
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

        // ── 모델 ───────────────────────────────────────────

        private void Show(ItemData item)
        {
            if (item == shown && model != null)
                return;
            shown = item;
            if (model != null)
                Destroy(model);
            model = null;
            useLength = 0f;
            if (item == null)
                return;

            motion = item is HoeData ? Motion.Swing : item is WateringCanData ? Motion.Tilt : item is MopData ? Motion.Push : Motion.Toss;
            model = item.HeldPrefab != null ? Instantiate(item.HeldPrefab, pivot, false) : BuildGreybox(item);
            foreach (var c in model.GetComponentsInChildren<Collider>())
                Destroy(c);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
