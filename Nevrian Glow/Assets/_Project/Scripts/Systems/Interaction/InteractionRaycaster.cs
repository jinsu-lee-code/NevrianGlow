using System;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NevrianGlowFarm.Systems.Interaction
{
    /// <summary>
    /// 화면 중앙 Raycast로 IInteractable을 찾고, 현재 도구로 프롬프트를 받아 알린다 (노션 GDD 12.3).
    /// 대상 · 도구가 바뀔 때와 상호작용 직후에만 프롬프트를 다시 받는다. 클릭 · E에 Interact(tool).
    /// </summary>
    public class InteractionRaycaster : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("Raycast 기준 — 1인칭 카메라")]
        [SerializeField] private Transform rayOrigin;
        [Tooltip("사거리 (Data/Stats/PlayerSettings의 상호작용 거리)")]
        [SerializeField] private PlayerSettingsData settings;
        [Tooltip("현재 도구를 주는 핫바")]
        [SerializeField] private Hotbar hotbar;
        [Tooltip("상호작용 입력 (Player/Interact, E)")]
        [SerializeField] private InputActionReference interactAction;
        [Tooltip("도구 사용 입력 (Player/Attack, 좌클릭)")]
        [SerializeField] private InputActionReference useAction;

        [Header("조정값")]
        [Tooltip("Raycast가 맞는 레이어 (Interactable)")]
        [SerializeField] private LayerMask interactableMask;

        [Header("기즈모")]
        [Tooltip("플레이 중 시안 직선 + 맞은 점 표시")]
        [SerializeField] private bool drawRay = true;

        [Header("디버그")]
        [Tooltip("지금 보고 있는 대상 (읽기 전용)")]
        [SerializeField] private string targetName;
        [Tooltip("지금 프롬프트 (읽기 전용)")]
        [SerializeField] private string prompt;

        private Collider lastCollider;
        private IInteractable target;
        private bool promptDirty = true;
        private bool hasHit;
        private Vector3 hitPoint;

        /// <summary>지금 보고 있는 대상. 없으면 null.</summary>
        public IInteractable Target => IsAlive(target) ? target : null;
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
            if (hotbar != null) hotbar.SelectionChanged += OnToolChanged;
            if (interactAction != null) interactAction.action.Enable();
            if (useAction != null) useAction.action.Enable();
            promptDirty = true;
        }

        private void OnDisable()
        {
            if (hotbar != null) hotbar.SelectionChanged -= OnToolChanged;
            SetPrompt(null);
        }

        private void Update()
        {
            UpdateTarget();

            if (promptDirty)
                RefreshPrompt();

            bool pressed = interactAction.action.WasPressedThisFrame() || useAction.action.WasPressedThisFrame();
            if (pressed && IsAlive(target) && !string.IsNullOrEmpty(prompt))
            {
                target.Interact(hotbar.SelectedTool);
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

            // 콜라이더가 바뀔 때만 컴포넌트를 찾는다 (매 프레임 GetComponent 금지).
            if (hitCollider == lastCollider && IsAlive(target) == (target != null))
                return;

            lastCollider = hitCollider;
            target = hitCollider != null ? hitCollider.GetComponentInParent<IInteractable>() : null;
            targetName = IsAlive(target) ? ((Component)target).name : string.Empty;
            promptDirty = true;
        }

        private void RefreshPrompt()
        {
            promptDirty = false;
            SetPrompt(IsAlive(target) ? target.GetPrompt(hotbar.SelectedTool) : null);
        }

        private void SetPrompt(string newPrompt)
        {
            newPrompt = string.IsNullOrEmpty(newPrompt) ? string.Empty : newPrompt;
            if (newPrompt == prompt)
                return;
            prompt = newPrompt;
            PromptChanged?.Invoke(prompt);
        }

        private void OnToolChanged(int index) => promptDirty = true;

        // 파괴된 MonoBehaviour는 인터페이스로 들고 있으면 null 비교가 통과하므로 Unity 기준으로 확인한다.
        private static bool IsAlive(IInteractable interactable) => interactable is UnityEngine.Object obj && obj != null;

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
