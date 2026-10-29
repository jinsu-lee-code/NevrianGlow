using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Farm;
using UnityEngine;

namespace NevrianGlowFarm.Systems.Farm
{
    /// <summary>
    /// 확장 밭 자리 (수정사항 10/29: 해금되는 밭을 맵 곳곳에 — 시작 밭과 같은 규격, 해금 전에는 울타리만, 상점에서 사면 밭이 나타남, 걸어서 이동).
    /// 이 오브젝트 위치 = 밭 사각형의 가운데(바닥 높이), 앞(+Z) = 줄 방향(across, 시작 밭의 '문에서 멀어지는 쪽'과 같은 뜻).
    /// 칸 번호 규칙은 시작 밭과 같다(FarmLayout) — FarmGrid가 시작 구획 다음 구획부터 자리 순서대로 이 자리에 칸을 놓는다.
    /// 울타리는 Glow Farm/Setup/Build Farm Field Sites가 자식(Fence)으로 만든다.
    /// </summary>
    public class FarmFieldSite : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("칸 간격 · 구획 크기 (Data/Stats/GameStartSettings) — 기즈모용, 게임에서는 FarmGrid의 설정을 쓴다")]
        [SerializeField] private GameStartSettingsData settings;

        [Header("조정값")]
        [Tooltip("이 자리의 구획 수 — 시작 밭과 같은 크기(4 × 2밭 = 32칸)면 2 (구획 = 16칸)")]
        [SerializeField, Min(1)] private int plots = 2;
        [Tooltip("상점에서 열었을 때 안내에 쓰는 이름 (예: 서쪽 밭)")]
        [SerializeField] private string displayName = "새 밭";
        [Tooltip("밭 둘레 길 폭 — 울타리는 이 바깥(Build Farm Field Sites), 열리면 이 안의 배경 풀을 지운다 (m)")]
        [SerializeField, Min(0f)] private float pathWidth = 1f;

        [Header("기즈모")]
        [Tooltip("밭 자리 사각형 (노랑 점선)")]
        [SerializeField] private bool drawField = true;

        public int Plots => plots;
        public string DisplayName => displayName;
        public float PathWidth => pathWidth;

        /// <summary>밭 + 둘레 길의 월드 x · z 사각형 (자리 방향이 90° 단위일 때 정확) — 열린 밭의 풀 지우기.</summary>
        public Rect GroundRect(GameStartSettingsData settings)
        {
            var h = HalfSize(settings);
            Vector3 extent = Along * (h.x + pathWidth) + Across * (h.y + pathWidth);
            Vector3 extent2 = Along * (h.x + pathWidth) - Across * (h.y + pathWidth);
            float hx = Mathf.Max(Mathf.Abs(extent.x), Mathf.Abs(extent2.x)), hz = Mathf.Max(Mathf.Abs(extent.z), Mathf.Abs(extent2.z));
            Vector3 c = transform.position;
            return new Rect(c.x - hx, c.z - hz, hx * 2f, hz * 2f);
        }

        /// <summary>줄 방향 (자리의 앞, 수평).</summary>
        public Vector3 Across
        {
            get
            {
                var f = transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        /// <summary>밭이 이어지는 방향 — 시작 밭과 같은 식(across = up × along).</summary>
        public Vector3 Along => Vector3.Cross(Across, Vector3.up).normalized;

        public Quaternion CellRotation => Quaternion.LookRotation(Across, Vector3.up);

        /// <summary>자리 안 칸 번호의 월드 위치 (바닥 높이 = 이 오브젝트 높이).</summary>
        public Vector3 CellPosition(int localIndex, GameStartSettingsData settings)
        {
            var b = FarmLayout.AreaCenterBounds(plots, settings.CellsPerPlot, settings.CellPitch, settings.BedPitch, settings.PlotPitch);
            var (along, across) = FarmLayout.CellOffset(localIndex, settings.CellsPerPlot, settings.CellPitch, settings.BedPitch, settings.PlotPitch);
            double a = along - (b.AlongMin + b.AlongMax) * 0.5;
            double c = across - (b.AcrossMin + b.AcrossMax) * 0.5;
            return transform.position + Along * (float)a + Across * (float)c;
        }

        /// <summary>밭 사각형의 (along, across) 반 크기 — 칸 바깥 가장자리까지 (m).</summary>
        public Vector2 HalfSize(GameStartSettingsData settings)
        {
            var b = FarmLayout.AreaCenterBounds(plots, settings.CellsPerPlot, settings.CellPitch, settings.BedPitch, settings.PlotPitch);
            float half = settings.CellPitch * 0.5f;
            return new Vector2((float)(b.AlongMax - b.AlongMin) * 0.5f + half, (float)(b.AcrossMax - b.AcrossMin) * 0.5f + half);
        }

        private void OnDrawGizmos()
        {
            if (!drawField || settings == null)
                return;
            var h = HalfSize(settings);
            float alongHalf = h.x, acrossHalf = h.y;
            Vector3 o = transform.position + Vector3.up * 0.1f;
            Vector3 Corner(float a, float c) => o + Along * a + Across * c;
            GizmoKit.DashedQuad(Corner(-alongHalf, -acrossHalf), Corner(alongHalf, -acrossHalf), Corner(alongHalf, acrossHalf), Corner(-alongHalf, acrossHalf), 0.5f, GizmoKit.HarvestDisplay);
            GizmoKit.Line(o, o + Across * 2f, GizmoKit.Spawn);
        }
    }
}
