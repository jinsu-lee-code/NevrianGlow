using NevrianGlowFarm.Core;
using NevrianGlowFarm.Systems.Farm;
using NevrianGlowFarm.Systems.Zones;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Player
{
    /// <summary>
    /// 플레이어 발소리 (사운드 납품 10.23: sfx_step_wood · soil · grass, 변형 3개). 걸은 거리마다 한 번.
    /// 바닥 소리는 지금 지역(ZoneProfileData.footstepId), 농장의 밭 · 길 위는 흙(SoundCues.StepSoil). 파일이 없으면 AudioManager가 무음으로 넘어간다.
    /// Core Player에 붙는다 (Glow Farm/Setup/Build Sound Library가 붙인다).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerFootsteps : MonoBehaviour
    {
        [Header("조정값")]
        [Tooltip("발소리 간격 — 걸은 거리 (m)")]
        [SerializeField, Min(0.2f)] private float stepDistance = 1.7f;
        [Tooltip("이보다 느리면 걷는 것으로 보지 않는다 (m/s)")]
        [SerializeField, Min(0f)] private float minSpeed = 0.5f;

        private PlayerController controller;
        private Vector3 last;
        private float travelled;
        private FarmGrid farm;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            last = transform.position;
        }

        private void Update()
        {
            Vector3 p = transform.position;
            Vector3 d = p - last;
            d.y = 0f;
            last = p;
            if (!controller.IsGrounded || controller.CurrentSpeed < minSpeed || d.magnitude > stepDistance * 2f) // 순간이동은 뺌
            {
                if (controller.CurrentSpeed < minSpeed)
                    travelled = 0f;
                return;
            }
            travelled += d.magnitude;
            if (travelled < stepDistance)
                return;
            travelled -= stepDistance;
            string id = SurfaceId(p);
            if (!string.IsNullOrEmpty(id))
                AudioManager.Play(id);
        }

        private string SurfaceId(Vector3 position)
        {
            var zone = ZoneTransition.Instance != null ? ZoneTransition.Instance.Current : null;
            if (zone == null)
                return null;
            if (zone.IsDayStart) // 농장: 밭 · 길 위는 흙 (FarmGrid는 Farm 씬 — 처음 쓸 때 한 번 찾는다)
            {
                if (farm == null)
                    farm = FindFirstObjectByType<FarmGrid>();
                if (farm != null && farm.IsFarmGround(position))
                    return SoundCues.StepSoil;
            }
            return zone.FootstepId;
        }
    }
}
