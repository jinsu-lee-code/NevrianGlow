using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace NevrianGlowFarm.Systems.Items
{
    /// <summary>
    /// 내려놓기 (GDD 4-3): Q = 고른 핫바 칸에서 1개, Shift+Q = 묶음 전체. 조준한 바닥 지점(손 닿는 거리 안)에 살며시 놓는다(던지기 아님).
    /// 바닥을 못 찾으면(허공 · 너무 멀다) 놓지 않는다. 놓인 아이템은 맞은 바닥의 구역 씬으로 옮긴다.
    /// Shift는 질주 키와 같다 (GDD 열린 항목 21 — 질주 중 Q는 묶음 전체).
    /// </summary>
    public class ItemDropper : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("Raycast 기준 — 1인칭 카메라")]
        [SerializeField] private Transform rayOrigin;
        [Tooltip("손 닿는 거리 = 상호작용 거리 (Data/Stats/PlayerSettings)")]
        [SerializeField] private PlayerSettingsData settings;
        [Tooltip("내려놓을 아이템을 고르는 핫바")]
        [SerializeField] private Hotbar hotbar;
        [Tooltip("바닥 아이템 프리팹 (Prefabs/Items/world_item)")]
        [SerializeField] private WorldItem worldItemPrefab;
        [Tooltip("내려놓기 입력 (Player/Drop, Q)")]
        [SerializeField] private InputActionReference dropAction;
        [Tooltip("묶음 전체 조합 키 (Player/Sprint, Shift)")]
        [SerializeField] private InputActionReference stackModifierAction;

        [Header("조정값")]
        [Tooltip("바닥으로 칠 레이어 — 상호작용 대상(Interactable)은 빼서 밭 칸 위가 아니라 땅에 놓는다")]
        [SerializeField] private LayerMask groundMask = 1;
        [Tooltip("바닥 위로 띄우는 높이 (m)")]
        [SerializeField, Min(0f)] private float lift = 0.02f;
        [Tooltip("벽 · 경사면에는 놓지 않는다 — 바닥 법선이 위쪽과 이루는 최대 각 (°)")]
        [SerializeField, Range(0f, 89f)] private float maxSlope = 35f;

        private void Awake()
        {
            if (rayOrigin == null || settings == null || hotbar == null || worldItemPrefab == null || dropAction == null || stackModifierAction == null)
            {
                Debug.LogError("[ItemDropper] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (dropAction != null) dropAction.action.Enable();
            if (stackModifierAction != null) stackModifierAction.action.Enable();
        }

        private void Update()
        {
            if (PlayerInputGate.IsWorldBlocked || !dropAction.action.WasPressedThisFrame())
                return;
            TryDropSelected(stackModifierAction.action.IsPressed());
        }

        /// <summary>고른 칸에서 1개(또는 묶음 전체)를 조준한 바닥에 놓는다. 놓았으면 그 아이템.</summary>
        public WorldItem TryDropSelected(bool wholeStack)
        {
            ItemData item = hotbar.SelectedItem;
            int have = hotbar.SelectedCount;
            if (item == null || have <= 0)
                return null;
            if (!FindGround(out RaycastHit hit))
                return null;

            int amount = wholeStack ? have : 1;
            if (!hotbar.TryConsumeSelected(amount))
                return null;

            // 바라보는 방향으로 돌려 놓는다 (살며시 — 물리 없음).
            Vector3 forward = Vector3.ProjectOnPlane(rayOrigin.forward, Vector3.up);
            var rotation = forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
            var dropped = Instantiate(worldItemPrefab, hit.point + Vector3.up * lift, rotation);
            dropped.Init(item, amount);
            Scene zone = hit.collider.gameObject.scene;
            if (zone.IsValid() && zone != dropped.gameObject.scene)
                SceneManager.MoveGameObjectToScene(dropped.gameObject, zone);
            return dropped;
        }

        // 플레이어 몸(CharacterController)도 같은 레이어라 가장 가까운 것부터 보며 자기 몸은 건너뛴다. 누를 때만 하므로 RaycastAll로 충분.
        private bool FindGround(out RaycastHit hit)
        {
            hit = default;
            var hits = Physics.RaycastAll(rayOrigin.position, rayOrigin.forward, settings.InteractMaxDistance, groundMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform.root))
                    continue;
                hit = h;
                return Vector3.Angle(h.normal, Vector3.up) <= maxSlope;
            }
            return false;
        }
    }
}
