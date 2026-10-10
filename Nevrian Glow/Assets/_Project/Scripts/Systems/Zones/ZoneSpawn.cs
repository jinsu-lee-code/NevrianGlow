using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Zones
{
    /// <summary>
    /// 지역에 들어올 때 플레이어가 서는 곳 (FarmSpawn · ShopSpawn, BALANCE 9-1). 위치 = 발 위치, 앞쪽(파란 축) = 바라보는 방향.
    /// 기즈모: 플레이어 캡슐(초록), 벽 · 물체와 겹치면 빨강 — 순간이동 후 끼지 않는지 배치할 때 바로 본다.
    /// </summary>
    public class ZoneSpawn : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("이 출발 지점이 속한 지역 (Data/Zones)")]
        [SerializeField] private ZoneProfileData zone;

        [Header("기즈모")]
        [Tooltip("검사 캡슐 반지름 — 플레이어 CharacterController와 같게 (m)")]
        [SerializeField, Min(0.05f)] private float capsuleRadius = 0.35f;
        [Tooltip("검사 캡슐 높이 — 플레이어 CharacterController와 같게 (m)")]
        [SerializeField, Min(0.1f)] private float capsuleHeight = 1.8f;
        [Tooltip("겹침 검사에서 뺄 레이어 (바닥은 캡슐 바닥이 살짝 띄워져 있어 안 걸림)")]
        [SerializeField] private LayerMask ignoreLayers;

        public ZoneProfileData Zone => zone;
        public Vector3 Position => transform.position;
        public float Yaw => transform.eulerAngles.y;

        /// <summary>이 자리에 플레이어 캡슐을 세우면 다른 콜라이더와 겹치는지 (플레이어 자신은 제외).</summary>
        public bool IsBlocked(Transform ignoreRoot = null)
        {
            const float skin = 0.05f; // 바닥에 닿는 것은 겹침이 아니다
            Vector3 bottom = transform.position + Vector3.up * (capsuleRadius + skin);
            Vector3 top = transform.position + Vector3.up * (capsuleHeight - capsuleRadius);
            var hits = Physics.OverlapCapsule(bottom, top, capsuleRadius, ~ignoreLayers, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                // 플레이어 몸(CharacterController)은 겹침이 아니다 — 에디터에서는 Core의 Player가 출발 지점에 서 있다.
                if (h is CharacterController || (ignoreRoot != null && h.transform.IsChildOf(ignoreRoot)))
                    continue;
                return true;
            }
            return false;
        }

        private void OnDrawGizmos()
        {
            Color c = IsBlocked() ? GizmoKit.Forbidden : GizmoKit.Spawn;
            Vector3 p = transform.position;
            GizmoKit.WirePoint(p + Vector3.up * capsuleRadius, capsuleRadius, c);
            GizmoKit.WirePoint(p + Vector3.up * (capsuleHeight - capsuleRadius), capsuleRadius, c);
            GizmoKit.Line(p + Vector3.up * capsuleRadius + Vector3.left * capsuleRadius, p + Vector3.up * (capsuleHeight - capsuleRadius) + Vector3.left * capsuleRadius, c);
            GizmoKit.Line(p + Vector3.up * capsuleRadius + Vector3.right * capsuleRadius, p + Vector3.up * (capsuleHeight - capsuleRadius) + Vector3.right * capsuleRadius, c);
            GizmoKit.Line(p + Vector3.up * 1f, p + Vector3.up * 1f + transform.forward * 1f, c);
        }
    }
}
