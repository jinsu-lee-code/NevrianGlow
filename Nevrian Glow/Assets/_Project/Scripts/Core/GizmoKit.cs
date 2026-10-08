using UnityEngine;

namespace NevrianGlowFarm.Core
{
    /// <summary>
    /// 기즈모는 이 클래스로만 그린다 (CLAUDE.md 코딩 규칙).
    /// 색 규칙: 시안=상호작용, 파랑=도구 범위, 초록=생성·출발, 마젠타=손님 경로, 노랑=수확·진열, 빨강=금지·겹침.
    /// </summary>
    public static class GizmoKit
    {
        public static readonly Color Interaction = new Color(0.38f, 0.95f, 0.88f);   // #62F2E1
        public static readonly Color ToolRange = new Color(0.30f, 0.55f, 1f);
        public static readonly Color Spawn = new Color(0.35f, 0.95f, 0.45f);
        public static readonly Color CustomerPath = new Color(1f, 0.45f, 0.85f);     // #FF74D8
        public static readonly Color HarvestDisplay = new Color(1f, 0.82f, 0.48f);   // #FFD27A
        public static readonly Color Forbidden = new Color(1f, 0.25f, 0.25f);

        public static void Line(Vector3 from, Vector3 to, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(from, to);
        }

        public static void Point(Vector3 position, float radius, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawSphere(position, radius);
        }

        public static void WirePoint(Vector3 position, float radius, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(position, radius);
        }
    }
}
