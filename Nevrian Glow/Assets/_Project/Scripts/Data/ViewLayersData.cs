using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 거리 구간별 배경 화면 (2026-10-31, 플레이 피드백 '배경이 너무 뭉개짐') — ViewLayers가 아트 화면 효과의 거리 값만 실행 중에 덮어쓴다.
    /// 근경(풀 · 밭) = 붓 터치로 정리, 중경(나무 · 절벽 · 등대) = 또렷하게, 원경(산 · 지평선) = 거리 안개 + 살짝 흐림.
    /// 효과의 색 · 세기는 아트 값 그대로 두고 '어디서부터'만 정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Settings/View Layers", fileName = "ViewLayers")]
    public class ViewLayersData : ScriptableObject
    {
        [Header("근경 — 붓 터치")]
        [Tooltip("가까운 풀 · 잎을 붓 터치로 정리하는 세기 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float brushStrength = 0.5f;
        [Tooltip("붓 터치가 가장 센 거리 — 여기까지가 근경 (m)")]
        [SerializeField, Min(0f)] private float nearEnd = 8f;
        [Tooltip("붓 터치가 다 빠지는 거리 — 이 너머 중경 나무 · 절벽은 원래 모양대로 (m)")]
        [SerializeField, Min(0f)] private float brushEnd = 35f;
        [Tooltip("붓 터치가 빠진 뒤 남는 세기 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float brushFarStrength;

        [Header("중경 → 원경 — 거리 안개")]
        [Tooltip("거리 안개가 시작되는 거리 — 여기까지 중경은 제 색 (m)")]
        [SerializeField, Min(0f)] private float hazeStart = 55f;
        [Tooltip("거리 안개가 가장 짙어지는 거리 (m)")]
        [SerializeField, Min(0f)] private float hazeEnd = 320f;
        [Tooltip("안개 속에서도 남기는 음영 — 높을수록 먼 산 · 나무 모양이 남는다 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float hazeDetailKeep = 0.8f;

        [Header("원경 — 초점 흐림")]
        [Tooltip("초점 흐림이 시작되는 거리 — 중경은 흐리지 않게 (m)")]
        [SerializeField, Min(0f)] private float blurStart = 90f;
        [Tooltip("초점 흐림이 가장 센 거리 (m)")]
        [SerializeField, Min(0f)] private float blurEnd = 320f;
        [Tooltip("가장 먼 곳의 흐림 크기 (URP 가우스 반경)")]
        [SerializeField, Range(0.5f, 1.5f)] private float blurRadius = 0.5f;

        [Header("외곽선 — 먼 배경은 빼기 (아트 피드백 10/30)")]
        [Tooltip("화면 외곽선이 옅어지기 시작하는 거리 (m)")]
        [SerializeField, Min(0f)] private float outlineFadeStart = 40f;
        [Tooltip("화면 외곽선이 다 사라지는 거리 — 이 너머 먼 배경(원경)에는 선을 긋지 않는다. 그림자 거리 · 나무 LOD1 시작과 맞춘다 (m)")]
        [SerializeField, Min(0f)] private float outlineFadeEnd = 80f;

        public float BrushStrength => brushStrength;
        public float NearEnd => nearEnd;
        public float BrushEnd => brushEnd;
        public float BrushFarStrength => brushFarStrength;
        public float HazeStart => hazeStart;
        public float HazeEnd => hazeEnd;
        public float HazeDetailKeep => hazeDetailKeep;
        public float BlurStart => blurStart;
        public float BlurEnd => blurEnd;
        public float BlurRadius => blurRadius;
        public float OutlineFadeStart => outlineFadeStart;
        public float OutlineFadeEnd => outlineFadeEnd;
    }
}
