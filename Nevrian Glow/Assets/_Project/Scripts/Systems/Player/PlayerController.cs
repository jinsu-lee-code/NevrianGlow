using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NevrianGlowFarm.Systems.Player
{
    /// <summary>
    /// 1인칭 이동 · 시점 · 중력 · 질주 · 헤드밥 (노션 GDD 12.3). 수치는 PlayerSettingsData, 계산은 Logic의 FirstPersonMath.
    /// 플레이어 몸이 yaw, 자식 카메라가 pitch와 눈높이 · 헤드밥을 맡는다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("이동 · 시점 수치 (Data/Stats/PlayerSettings)")]
        [SerializeField] private PlayerSettingsData settings;
        [Tooltip("1인칭 카메라 — 플레이어의 자식")]
        [SerializeField] private Transform cameraPivot;
        [Tooltip("구역 씬이 다 올라오기 전에는 움직이지 않는다 — 비우면 바로 시작")]
        [SerializeField] private SceneBootstrapper sceneBootstrapper;
        [Tooltip("이동 입력 (Player/Move)")]
        [SerializeField] private InputActionReference moveAction;
        [Tooltip("시점 입력 (Player/Look)")]
        [SerializeField] private InputActionReference lookAction;
        [Tooltip("질주 입력 (Player/Sprint)")]
        [SerializeField] private InputActionReference sprintAction;

        [Header("조정값")]
        [Tooltip("땅에 서 있을 때 아래로 붙여 두는 속도 — 내리막에서 뜨지 않게 (m/s)")]
        [SerializeField, Min(0f)] private float groundStickSpeed = 2f;
        [Tooltip("플레이 중 마우스 커서를 가두고 숨긴다")]
        [SerializeField] private bool lockCursor = true;

        [Header("디버그")]
        [Tooltip("현재 수평 이동 속도 (m/s, 읽기 전용)")]
        [SerializeField] private float currentSpeed;
        [Tooltip("땅에 닿아 있는지 (읽기 전용)")]
        [SerializeField] private bool isGrounded;

        private CharacterController controller;
        private float yaw;
        private float pitch;
        private float verticalVelocity;
        private float bobPhase;
        private bool zonesReady;

        public bool IsGrounded => isGrounded;
        public float CurrentSpeed => currentSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (settings == null || cameraPivot == null || moveAction == null || lookAction == null || sprintAction == null)
            {
                Debug.LogError("[PlayerController] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }

            yaw = transform.eulerAngles.y;
            pitch = Mathf.DeltaAngle(0f, cameraPivot.localEulerAngles.x);
        }

        private void OnEnable()
        {
            if (moveAction != null) moveAction.action.Enable();
            if (lookAction != null) lookAction.action.Enable();
            if (sprintAction != null) sprintAction.action.Enable();

            PlayerInputGate.Changed += OnGateChanged;
            ApplyCursor(PlayerInputGate.IsBlocked);
        }

        private void OnDisable()
        {
            PlayerInputGate.Changed -= OnGateChanged;
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>
        /// 순간이동 (장소 이동, CLAUDE 핵심 규칙 8): CharacterController를 끄고 → 위치 지정 → SyncTransforms → 다시 켠다.
        /// 바라보는 방향도 정하고(위아래 시점은 정면으로), 떨어지던 속도는 버린다.
        /// </summary>
        public void TeleportTo(Vector3 position, float newYaw)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, newYaw, 0f));
            Physics.SyncTransforms();
            controller.enabled = true;

            yaw = newYaw;
            pitch = 0f;
            verticalVelocity = 0f;
            cameraPivot.localRotation = Quaternion.identity;
        }

        private void OnGateChanged(bool blocked) => ApplyCursor(blocked);

        // 화면(가방 · 창고 등)이 열려 조작이 잠기면 커서를 풀어 보이게 한다.
        private void ApplyCursor(bool blocked)
        {
            if (!lockCursor)
                return;
            Cursor.lockState = blocked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = blocked;
        }

        private void Update()
        {
            if (!zonesReady)
            {
                zonesReady = sceneBootstrapper == null || sceneBootstrapper.IsReady;
                if (!zonesReady)
                    return;
            }

            // 조작이 잠겨도 중력은 계속 받는다 (입력만 0).
            bool blocked = PlayerInputGate.IsBlocked;
            if (!blocked)
                UpdateLook();
            Vector2 moveInput = UpdateMove(blocked);
            UpdateHeadBob(moveInput);
        }

        private void UpdateLook()
        {
            Vector2 delta = lookAction.action.ReadValue<Vector2>();
            FirstPersonMath.ApplyLook(ref yaw, ref pitch, delta.x, delta.y, settings.LookSensitivity, settings.PitchLimit);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private Vector2 UpdateMove(bool blocked)
        {
            Vector2 input = blocked ? Vector2.zero : Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
            bool sprinting = !blocked && sprintAction.action.IsPressed();
            float speed = settings.MoveSpeed * (sprinting ? settings.SprintMultiplier : 1f);

            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -groundStickSpeed;
            else
                verticalVelocity += Physics.gravity.y * Time.deltaTime;

            controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);

            isGrounded = controller.isGrounded;
            currentSpeed = horizontal.magnitude;
            return input;
        }

        private void UpdateHeadBob(Vector2 moveInput)
        {
            bool moving = isGrounded && moveInput.sqrMagnitude > 0.01f;
            float speedRatio = settings.MoveSpeed > 0f ? currentSpeed / settings.MoveSpeed : 0f;
            bobPhase = FirstPersonMath.AdvanceBobPhase(bobPhase, settings.HeadBobFrequency, speedRatio, Time.deltaTime, moving);

            float bob = FirstPersonMath.BobOffset(bobPhase, settings.HeadBobAmplitude);
            cameraPivot.localPosition = new Vector3(0f, settings.EyeHeight + bob, 0f);
        }
    }
}
