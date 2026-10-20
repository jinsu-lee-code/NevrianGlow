using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NevrianGlowFarm.Systems.Cleaning
{
    /// <summary>
    /// 대걸레 (GDD 4-4): 대걸레를 들고 좌클릭을 누르고 있는 동안, 조준한 바닥 지점 반경 안의 얼룩을 닦는다.
    /// 반경 · 속도는 든 대걸레 등급(MopData). 조작이 잠겨 있으면(화면 · 장소 이동) 닦지 않는다.
    /// </summary>
    public class MopTool : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("Raycast 기준 — 1인칭 카메라")]
        [SerializeField] private Transform rayOrigin;
        [Tooltip("손 닿는 거리 = 상호작용 거리 (Data/Stats/PlayerSettings)")]
        [SerializeField] private PlayerSettingsData settings;
        [Tooltip("든 아이템을 알려 주는 핫바")]
        [SerializeField] private Hotbar hotbar;
        [Tooltip("사용 입력 (Player/Attack, 좌클릭) — 누르고 있는 동안")]
        [SerializeField] private InputActionReference useAction;

        [Header("조정값")]
        [Tooltip("바닥으로 칠 레이어 (Default)")]
        [SerializeField] private LayerMask groundMask = 1;

        [Header("기즈모")]
        [Tooltip("플레이 중 닦는 범위(파랑) 표시")]
        [SerializeField] private bool drawRange = true;

        [Header("디버그")]
        [Tooltip("지금 닦고 있는지 (읽기 전용)")]
        [SerializeField] private bool mopping;

        private bool hasPoint;
        private Vector3 point;
        private float radius;
        private int mopLoop;

        public bool IsMopping => mopping;

        private void Awake()
        {
            if (rayOrigin == null || settings == null || hotbar == null || useAction == null)
            {
                Debug.LogError("[MopTool] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (useAction != null) useAction.action.Enable();
        }

        private void OnDisable()
        {
            mopping = false;
            UpdateMopSound();
        }

        private void Update()
        {
            UpdateMopping();
            UpdateMopSound();
        }

        // 닦는 동안 루프 소리, 손을 떼면 짧게 줄이며 멈춘다 (asset_placement 6번).
        private void UpdateMopSound()
        {
            if (mopping && mopLoop == 0)
                mopLoop = AudioManager.StartLoop(SoundCues.MopLoop);
            else if (!mopping && mopLoop != 0)
            {
                AudioManager.StopLoop(mopLoop);
                mopLoop = 0;
            }
        }

        private void UpdateMopping()
        {
            mopping = false;
            hasPoint = false;
            if (PlayerInputGate.IsBlocked || !(hotbar.SelectedItem is MopData mop))
                return;

            hasPoint = FindGround(out point);
            radius = mop.Radius;
            if (!hasPoint || !useAction.action.IsPressed())
                return;

            var cleaning = CleaningSystem.Instance;
            if (cleaning != null)
            {
                cleaning.Mop(point, mop.Radius, mop.Speed * Time.deltaTime);
                mopping = true;
            }
        }

        /// <summary>조준한 바닥 지점 (자기 몸은 건너뜀).</summary>
        public bool FindGround(out Vector3 groundPoint)
        {
            groundPoint = default;
            var hits = Physics.RaycastAll(rayOrigin.position, rayOrigin.forward, settings.InteractMaxDistance, groundMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform.root))
                    continue;
                groundPoint = h.point;
                return true;
            }
            return false;
        }

        private void OnDrawGizmos()
        {
            if (drawRange && Application.isPlaying && hasPoint)
                GizmoKit.WirePoint(point, radius, GizmoKit.ToolRange);
        }
    }
}
