using System;
using System.Text;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NevrianGlowFarm.Systems.Interaction
{
    /// <summary>
    /// 화면 중앙 Raycast로 대상을 찾는다 (노션 GDD 12.3, GDD 4-3).
    /// E = 손 동작(IHandInteractable, 든 아이템과 무관) / 좌클릭 = 든 아이템 사용(IItemUseTarget) / 우클릭 = 보조 사용(ISecondaryUseTarget).
    /// 한 대상이 여럿을 가질 수 있다. 조준점이 필요한 대상(IAimPointTarget, 진열대)에는 보는 동안 매 프레임 점을 알려 준다.
    /// 대상 · 든 아이템이 바뀔 때, 상호작용 직후, 대상이 요청할 때만 프롬프트를 다시 받는다.
    /// </summary>
    public class InteractionRaycaster : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("Raycast 기준 — 1인칭 카메라")]
        [SerializeField] private Transform rayOrigin;
        [Tooltip("사거리 (Data/Stats/PlayerSettings의 상호작용 거리)")]
        [SerializeField] private PlayerSettingsData settings;
        [Tooltip("든 아이템을 주는 핫바")]
        [SerializeField] private Hotbar hotbar;
        [Tooltip("손 동작 입력 (Player/Interact, E)")]
        [SerializeField] private InputActionReference interactAction;
        [Tooltip("든 아이템 사용 입력 (Player/Attack, 좌클릭)")]
        [SerializeField] private InputActionReference useAction;
        [Tooltip("보조 사용 입력 (Player/Retrieve, 우클릭)")]
        [SerializeField] private InputActionReference secondaryAction;

        [Header("조정값")]
        [Tooltip("Raycast가 맞는 레이어 (Interactable)")]
        [SerializeField] private LayerMask interactableMask;
        [Tooltip("프롬프트에 붙이는 손 동작 키 이름")]
        [SerializeField] private string handKeyLabel = "E";
        [Tooltip("프롬프트에 붙이는 아이템 사용 키 이름")]
        [SerializeField] private string useKeyLabel = "좌클릭";
        [Tooltip("프롬프트에 붙이는 보조 사용 키 이름")]
        [SerializeField] private string secondaryKeyLabel = "우클릭";

        [Header("기즈모")]
        [Tooltip("플레이 중 시안 직선 + 맞은 점 표시")]
        [SerializeField] private bool drawRay = true;

        [Header("디버그")]
        [Tooltip("지금 보고 있는 대상 (읽기 전용)")]
        [SerializeField] private string targetName;
        [Tooltip("지금 프롬프트 (읽기 전용)")]
        [SerializeField] private string prompt;

        private Collider lastCollider;
        private IHandInteractable handTarget;
        private IItemUseTarget useTarget;
        private ISecondaryUseTarget secondaryTarget;
        private IAimPointTarget aimTarget;
        private IAimInfoTarget infoTarget;
        private string handPrompt;
        private string usePrompt;
        private string secondaryPrompt;
        private bool promptDirty = true;
        private bool hasHit;
        private Vector3 hitPoint;
        private readonly StringBuilder builder = new StringBuilder();

        /// <summary>지금 보고 있는 E 대상. 없으면 null.</summary>
        public IHandInteractable HandTarget => IsAlive(handTarget) ? handTarget : null;
        /// <summary>지금 보고 있는 좌클릭 대상. 없으면 null.</summary>
        public IItemUseTarget UseTarget => IsAlive(useTarget) ? useTarget : null;
        /// <summary>지금 보고 있는 우클릭 대상. 없으면 null.</summary>
        public ISecondaryUseTarget SecondaryTarget => IsAlive(secondaryTarget) ? secondaryTarget : null;
        public string Prompt => prompt;
        public bool HasHit => hasHit;
        public Vector3 HitPoint => hitPoint;

        /// <summary>프롬프트가 바뀔 때만 발생. 비어 있으면 숨긴다.</summary>
        public event Action<string> PromptChanged;

        private static int refreshRequestedFrame = -1;

        /// <summary>
        /// 보고 있는 대상의 상태가 상호작용 없이 바뀌었을 때(누르고 있는 동안 닦이는 얼룩 등) 이번 프레임에 프롬프트를 다시 받게 한다.
        /// </summary>
        public static void RequestPromptRefresh() => refreshRequestedFrame = Time.frameCount;

        /// <summary>좌클릭으로 대상에 아이템을 썼을 때 (든 아이템) — 1인칭 도구 동작(HeldItemView)이 듣는다.</summary>
        public event Action<ItemData> ItemUsed;

        private void Awake()
        {
            if (rayOrigin == null || settings == null || hotbar == null || interactAction == null || useAction == null || secondaryAction == null)
            {
                Debug.LogError("[InteractionRaycaster] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (hotbar != null)
            {
                hotbar.SelectionChanged += OnHeldItemChanged;
                hotbar.SlotsChanged += OnSlotsChanged;
            }
            if (interactAction != null) interactAction.action.Enable();
            if (useAction != null) useAction.action.Enable();
            if (secondaryAction != null) secondaryAction.action.Enable();
            GameEvents.PhaseChanged += OnPhaseChanged;
            promptDirty = true;
        }

        private void OnDisable()
        {
            if (hotbar != null)
            {
                hotbar.SelectionChanged -= OnHeldItemChanged;
                hotbar.SlotsChanged -= OnSlotsChanged;
            }
            GameEvents.PhaseChanged -= OnPhaseChanged;
            ClearAimTarget();
            SetPrompt(null);
        }

        private void Update()
        {
            // 화면(가방 · 창고)이 열려 조작이 잠기거나 장사 중(세상 조작 잠금, P-7)이면 프롬프트 · 홀로그램을 숨기고 입력을 받지 않는다. 풀리면 다시 받는다.
            if (PlayerInputGate.IsWorldBlocked)
            {
                ClearAimTarget();
                // 대상도 함께 비운다 — 구역 이동(문 · 다음 날) 뒤 ray가 아무것도 안 맞으면 "콜라이더 그대로(null)"로 보고
                // 이전 구역의 대상(밭 칸)이 남아 가게 안에서 "괭이로 갈기" 프롬프트가 뜨고 좌클릭이 그 칸에 들어갔다 (발견 2026-10-24)
                lastCollider = null;
                handTarget = null;
                useTarget = null;
                secondaryTarget = null;
                infoTarget = null;
                targetName = string.Empty;
                SetPrompt(null);
                promptDirty = true;
                return;
            }

            UpdateTarget();

            if (IsAlive(aimTarget) && aimTarget.UpdateAim(hitPoint, hotbar.SelectedItem))
                promptDirty = true;
            // 이번 프레임이나 지난 프레임의 요청 (실행 순서와 상관없이 한 번은 받는다)
            if (refreshRequestedFrame >= Time.frameCount - 1)
                promptDirty = true;

            if (promptDirty)
                RefreshPrompt();

            var held = hotbar.SelectedItem;
            if (interactAction.action.WasPressedThisFrame() && IsAlive(handTarget) && !string.IsNullOrEmpty(handPrompt))
            {
                handTarget.InteractHand();
                promptDirty = true;
            }

            if (useAction.action.WasPressedThisFrame() && IsAlive(useTarget) && !string.IsNullOrEmpty(usePrompt))
            {
                useTarget.Use(held);
                promptDirty = true;
                ItemUsed?.Invoke(held);
            }

            if (secondaryAction.action.WasPressedThisFrame() && IsAlive(secondaryTarget) && !string.IsNullOrEmpty(secondaryPrompt))
            {
                secondaryTarget.UseSecondary(held);
                promptDirty = true;
            }
        }

        private void UpdateTarget()
        {
            Collider hitCollider = null;
            hasHit = Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit,
                settings.InteractMaxDistance, interactableMask, QueryTriggerInteraction.Collide);
            if (hasHit)
            {
                hitCollider = hit.collider;
                hitPoint = hit.point;
            }

            // 콜라이더가 바뀔 때(또는 대상이 파괴됐을 때)만 컴포넌트를 찾는다 (매 프레임 GetComponent 금지).
            bool targetsAlive = IsAlive(handTarget) == (handTarget != null) && IsAlive(useTarget) == (useTarget != null)
                && IsAlive(secondaryTarget) == (secondaryTarget != null) && IsAlive(aimTarget) == (aimTarget != null);
            if (hitCollider == lastCollider && targetsAlive)
                return;

            lastCollider = hitCollider;
            var newAim = hitCollider != null ? hitCollider.GetComponentInParent<IAimPointTarget>() : null;
            if (!ReferenceEquals(newAim, aimTarget))
                ClearAimTarget();
            aimTarget = newAim;
            handTarget = hitCollider != null ? hitCollider.GetComponentInParent<IHandInteractable>() : null;
            useTarget = hitCollider != null ? hitCollider.GetComponentInParent<IItemUseTarget>() : null;
            secondaryTarget = hitCollider != null ? hitCollider.GetComponentInParent<ISecondaryUseTarget>() : null;
            infoTarget = hitCollider != null ? hitCollider.GetComponentInParent<IAimInfoTarget>() : null;
            var shown = FirstAlive(handTarget, useTarget, secondaryTarget);
            targetName = shown != null ? shown.name : string.Empty;
            promptDirty = true;
        }

        private void ClearAimTarget()
        {
            if (IsAlive(aimTarget))
                aimTarget.ClearAim();
            aimTarget = null;
        }

        private void RefreshPrompt()
        {
            promptDirty = false;
            var held = hotbar.SelectedItem;
            handPrompt = IsAlive(handTarget) ? handTarget.GetHandPrompt() : null;
            usePrompt = IsAlive(useTarget) ? useTarget.GetUsePrompt(held) : null;
            secondaryPrompt = IsAlive(secondaryTarget) ? secondaryTarget.GetSecondaryPrompt(held) : null;

            builder.Clear();
            Append(useKeyLabel, usePrompt);
            Append(secondaryKeyLabel, secondaryPrompt);
            Append(handKeyLabel, handPrompt);
            // 정보 한 줄 (키 없음) — 자라는 작물의 성장 정보
            string info = IsAlive(infoTarget) ? infoTarget.GetAimInfo() : null;
            if (!string.IsNullOrEmpty(info))
            {
                if (builder.Length > 0)
                    builder.Append('\n');
                builder.Append(info);
            }
            SetPrompt(builder.ToString());
        }

        private void Append(string key, string line)
        {
            if (string.IsNullOrEmpty(line))
                return;
            if (builder.Length > 0)
                builder.Append('\n');
            builder.Append('[').Append(key).Append("] ").Append(line);
        }

        private void SetPrompt(string newPrompt)
        {
            newPrompt = string.IsNullOrEmpty(newPrompt) ? string.Empty : newPrompt;
            if (newPrompt == prompt)
                return;
            prompt = newPrompt;
            PromptChanged?.Invoke(prompt);
        }

        private void OnHeldItemChanged(int index) => promptDirty = true;
        private void OnSlotsChanged() => promptDirty = true;
        // 밤이 지나면 보고 있던 칸이 다 자랄 수 있다 → 단계가 바뀔 때도 다시 받는다.
        private void OnPhaseChanged(Logic.Game.PhaseChange change) => promptDirty = true;

        private static Component FirstAlive(params object[] targets)
        {
            foreach (var t in targets)
                if (IsAlive(t))
                    return (Component)t;
            return null;
        }

        // 파괴된 MonoBehaviour는 인터페이스로 들고 있으면 null 비교가 통과하므로 Unity 기준으로 확인한다.
        private static bool IsAlive(object target) => target is UnityEngine.Object obj && obj != null;

        private void OnDrawGizmos()
        {
            if (!drawRay || !Application.isPlaying || rayOrigin == null || settings == null)
                return;

            Vector3 end = hasHit ? hitPoint : rayOrigin.position + rayOrigin.forward * settings.InteractMaxDistance;
            GizmoKit.Line(rayOrigin.position, end, GizmoKit.Interaction);
            if (hasHit)
                GizmoKit.Point(hitPoint, 0.05f, GizmoKit.Interaction);
        }
    }
}
