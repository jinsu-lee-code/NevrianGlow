using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>플레이어 이동 · 시점 · 상호작용 (BALANCE 2-1, 노션 GDD 12.3).</summary>
    [CreateAssetMenu(menuName = "Glow Farm/Settings/Player", fileName = "PlayerSettings")]
    public class PlayerSettingsData : ScriptableObject
    {
        [Header("이동")]
        [Tooltip("걷기 속도 (m/s)")]
        [SerializeField, Range(2f, 8f)] private float moveSpeed = 2f;
        [Tooltip("질주 시 속도 배율 (배)")]
        [SerializeField, Range(1f, 2.5f)] private float sprintMultiplier = 1f;

        [Header("시점")]
        [Tooltip("마우스 감도 (°/px)")]
        [SerializeField, Range(0.05f, 0.5f)] private float lookSensitivity = 0.05f;
        [Tooltip("위아래 시점 제한 (°)")]
        [SerializeField, Range(60f, 89f)] private float pitchLimit = 60f;
        [Tooltip("눈높이 (m)")]
        [SerializeField, Range(1.4f, 1.9f)] private float eyeHeight = 1.4f;
        [Tooltip("헤드밥 진폭 (m)")]
        [SerializeField, Range(0f, 0.03f)] private float headBobAmplitude;
        [Tooltip("헤드밥 주파수 (Hz)")]
        [SerializeField, Range(0.5f, 2f)] private float headBobFrequency = 0.5f;
        [Tooltip("1인칭 카메라 세로 시야각 — 0이면 씬의 카메라 값 그대로 (°, 수정사항 10/29: 65)")]
        [SerializeField, Range(0f, 90f)] private float fieldOfView = 65f;

        [Header("상호작용")]
        [Tooltip("화면 중앙 Raycast 최대 거리 (m)")]
        [SerializeField, Range(1f, 8f)] private float interactMaxDistance = 1f;
        [Tooltip("E를 꾹 눌러야 하는 대상(수확)의 누르는 시간 — 화면 가운데 고리가 차오른다 (초, 기획서 v2.0: 0.5)")]
        [SerializeField, Range(0f, 2f)] private float handHoldSeconds = 0.5f;

        public float MoveSpeed => moveSpeed;
        public float SprintMultiplier => sprintMultiplier;
        public float LookSensitivity => lookSensitivity;
        public float PitchLimit => pitchLimit;
        public float EyeHeight => eyeHeight;
        public float HeadBobAmplitude => headBobAmplitude;
        public float HeadBobFrequency => headBobFrequency;
        public float FieldOfView => fieldOfView;
        public float InteractMaxDistance => interactMaxDistance;
        public float HandHoldSeconds => handHoldSeconds;
    }
}
