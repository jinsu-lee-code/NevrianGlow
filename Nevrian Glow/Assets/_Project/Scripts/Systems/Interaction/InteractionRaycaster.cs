using System;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NevrianGlowFarm.Systems.Interaction
{
    /// <summary>
    /// 화면 중앙 Raycast로 대상을 찾는다 (노션 GDD 12.3, GDD 4-3).
    /// E = 손 동작(IHandInteractable, 든 아이템과 무관) / 좌클릭 = 든 아이템 사용(IItemUseTarget). 한 대상이 둘 다 가질 수 있다.
    /// 대상 · 든 아이템이 바뀔 때와 상호작용 직후에만 프롬프트를 다시 받는다.
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

        [Header("조정값")]
        [Tooltip("Raycast가 맞는 레이어 (Interactable)")]
        [SerializeField] private LayerMask interactableMask;
        [Tooltip("프롬프트에 붙이는 손 동작 키 이름")]
        [SerializeField] private string handKeyLabel = "E";
        [Tooltip("프롬프트에 붙이는 아이템 사용 키 이름")]
        [SerializeField] private string useKeyLabel = "좌클릭";

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
        private string handPrompt;
        private string usePrompt;
        private bool promptDirty = true;
        private bool hasHit;
        private Vector3 hitPoint;

        /// <summary>지금 보고 있는 E 대상. 없으면 null.</summary>
        public IHandInteractable HandTarget => IsAlive(handTarget) ? handTarget : null;
        /// <summary>지금 보고 있는 좌클릭 대상. 없으면 null.</summary>
        public IItemUseTarget UseTarget => IsAlive(useTarget) ? useTarget : null;
        public string Prompt => prompt;

        /// <summary>프롬프트가 바뀔 때만 발생. 비어 있으면 숨긴다.</summary>
        public event Action<string> PromptChanged;

        private void Awake()
        {
            if (rayOrigin == null || settings == null || hotbar == null || interactAction == null || useAction == null)
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
            SetPrompt(null);
        }

        private void Update()
        {
            // 화면(가방 · 창고)이 열려 조작이 잠기면 프롬프트를 숨기고 입력을 받지 않는다. 닫히면 다시 받는다.
            if (PlayerInputGate.IsBlocked)
            {
                SetPrompt(null);
                promptDirty = true;
                return;
            }

            UpdateTarget();

            if (promptDirty)
                RefreshPrompt();

            if (interactAction.action.WasPressedThisFrame() && IsAlive(handTarget) && !string.IsNullOrEmpty(handPrompt))
            {
                handTarget.InteractHand();
                promptDirty = true;
            }

            if (useAction.action.WasPressedThisFrame() && IsAlive(useTarget) && !string.IsNullOrEmpty(usePrompt))
            {
                useTarget.Use(hotbar.SelectedItem);
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
            bool targetsAlive = IsAlive(handTarget) == (handTarget != null) && IsAlive(useTarget) == (useTarget != null);
            if (hitCollider == lastCollider && targetsAlive)
                return;

            lastCollider = hitCollider;
            handTarget = hitCollider != null ? hitCollider.GetComponentInParent<IHandInteractable>() : null;
            useTarget = hitCollider != null ? hitCollider.GetComponentInParent<IItemUseTarget>() : null;
            var shown = IsAlive(handTarget) ? (Component)handTarget : IsAlive(useTarget) ? (Component)useTarget : null;
            targetName = shown != null ? shown.name : string.Empty;
            promptDirty = true;
        }

        private void RefreshPrompt()
        {
            promptDirty = false;
            handPrompt = IsAlive(handTarget) ? handTarget.GetHandPrompt() : null;
            usePrompt = IsAlive(useTarget) ? useTarget.GetUsePrompt(hotbar.SelectedItem) : null;

            bool hasHand = !string.IsNullOrEmpty(handPrompt);
            bool hasUse = !string.IsNullOrEmpty(usePrompt);
            string combined = null;
            if (hasUse && hasHand)
                combined = $"[{useKeyLabel}] {usePrompt}\n[{handKeyLabel}] {handPrompt}";
            else if (hasUse)
                combined = $"[{useKeyLabel}] {usePrompt}";
            else if (hasHand)
                combined = $"[{handKeyLabel}] {handPrompt}";
            SetPrompt(combined);
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
