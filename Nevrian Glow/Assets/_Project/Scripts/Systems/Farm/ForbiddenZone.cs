using NevrianGlowFarm.Core;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Farm
{
    /// <summary>
    /// 밭 · 쓰레기를 놓으면 안 되는 구역 (BALANCE 9-1: 길, 문 주변 반경 등). 바닥 기준 원 또는 상자.
    /// 위치는 이 오브젝트의 위치, 회전은 쓰지 않는다. 레벨 배치도가 오면 오브젝트만 옮긴다.
    /// </summary>
    public class ForbiddenZone : MonoBehaviour
    {
        [Header("조정값")]
        [Tooltip("원 반경 — 0보다 크면 원, 0이면 아래 상자 크기를 쓴다 (m)")]
        [SerializeField, Min(0f)] private float radius;
        [Tooltip("상자 크기 x · z (m) — 원 반경이 0일 때만")]
        [SerializeField] private Vector2 boxSize = Vector2.one;
        [Tooltip("켜면 얼룩 · 발자국은 이 구역에도 생긴다 — 쓰레기 · 흩어진 상품은 그대로 막음 (가게 통로, 기획 회신 10/22 A2)")]
        [SerializeField] private bool allowsStains;

        public bool AllowsStains => allowsStains;

        /// <summary>바닥에서 본 구역을 감싸는 사각형 (x, z — 원은 지름 정사각형).</summary>
        public Rect FloorRect
        {
            get
            {
                Vector3 p = transform.position;
                Vector2 size = radius > 0f ? new Vector2(radius * 2f, radius * 2f) : boxSize;
                return new Rect(p.x - size.x * 0.5f, p.z - size.y * 0.5f, size.x, size.y);
            }
        }

        // 가장자리가 딱 닿는 것은 겹침이 아니다 (소수 오차로 -1.7999 < 1.8이 되는 것을 막는 허용치, m).
        private const float TouchTolerance = 0.01f;

        /// <summary>바닥에서 본 사각형(center, halfX, halfZ)이 이 구역과 겹치는지. 닿기만 하면 겹치지 않음.</summary>
        public bool Overlaps(Vector3 center, float halfX, float halfZ)
        {
            Vector3 p = transform.position;
            if (radius > 0f)
            {
                // 사각형 안에서 원 중심에 가장 가까운 점까지의 거리
                float dx = Mathf.Max(Mathf.Abs(center.x - p.x) - halfX, 0f);
                float dz = Mathf.Max(Mathf.Abs(center.z - p.z) - halfZ, 0f);
                float r = radius - TouchTolerance;
                return dx * dx + dz * dz < r * r;
            }
            return Mathf.Abs(center.x - p.x) < halfX + boxSize.x * 0.5f - TouchTolerance
                && Mathf.Abs(center.z - p.z) < halfZ + boxSize.y * 0.5f - TouchTolerance;
        }

        private void OnDrawGizmos()
        {
            if (radius > 0f)
                GizmoKit.WirePoint(transform.position, radius, GizmoKit.Forbidden);
            else
                GizmoKit.FlatBox(transform.position, new Vector3(boxSize.x, 0f, boxSize.y), GizmoKit.Forbidden, 0.12f);
        }
    }
}
