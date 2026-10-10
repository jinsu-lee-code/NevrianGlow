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

        public static void WireBox(Vector3 center, Vector3 size, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);
        }

        /// <summary>바닥에 눕힌 반투명 사각형 (칸 상태 표시용). size는 x · z만 쓴다.</summary>
        public static void FlatBox(Vector3 center, Vector3 size, Color color, float alpha = 0.35f)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, alpha);
            Gizmos.DrawCube(center, new Vector3(size.x, 0.02f, size.z));
            Gizmos.color = color;
            Gizmos.DrawWireCube(center, new Vector3(size.x, 0.02f, size.z));
        }

        public static void DashedLine(Vector3 from, Vector3 to, float dash, Color color)
        {
            Gizmos.color = color;
            float length = Vector3.Distance(from, to);
            if (length <= 0f || dash <= 0f)
                return;
            Vector3 dir = (to - from) / length;
            for (float t = 0f; t < length; t += dash * 2f)
                Gizmos.DrawLine(from + dir * t, from + dir * Mathf.Min(t + dash, length));
        }

        /// <summary>바닥 점선 사각형. 네 꼭짓점을 순서대로 받는다.</summary>
        public static void DashedQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float dash, Color color)
        {
            DashedLine(a, b, dash, color);
            DashedLine(b, c, dash, color);
            DashedLine(c, d, dash, color);
            DashedLine(d, a, dash, color);
        }
    }
}
