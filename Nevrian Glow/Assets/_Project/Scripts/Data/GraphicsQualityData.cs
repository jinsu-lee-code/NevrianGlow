using System.Collections.Generic;
using UnityEngine;

namespace NevrianGlowFarm.Data
{
    /// <summary>
    /// 그래픽 품질 단계 (최적화 2026-10-30) — 설정 화면의 '그래픽 품질'이 고르는 값 묶음. RenderBudget이 실행 중에 적용한다.
    /// 아트가 정한 값(URP 에셋 · 지형)은 '높음'으로 두고, 나머지 단계는 그 값을 줄인다 (풀 거리 · 밀도는 지형마다 아트 값에 곱함).
    /// </summary>
    [CreateAssetMenu(menuName = "Glow Farm/Settings/Graphics Quality", fileName = "GraphicsQuality")]
    public class GraphicsQualityData : ScriptableObject
    {
        [System.Serializable]
        public class Tier
        {
            [Tooltip("설정 화면에 보이는 이름 (예: 보통)")]
            [SerializeField] private string displayName = "보통";

            [Header("해 그림자")]
            [Tooltip("그림자가 그려지는 거리 (m)")]
            [SerializeField, Min(0f)] private float shadowDistance = 80f;
            [Tooltip("그림자 지도 해상도 — 1024 · 2048 · 4096 (픽셀)")]
            [SerializeField, Min(256)] private int shadowResolution = 2048;
            [Tooltip("그림자 단계 수 — 많을수록 가까운 그림자가 또렷하고 무겁다 (단계)")]
            [SerializeField, Range(1, 4)] private int shadowCascades = 3;

            [Header("풀")]
            [Tooltip("풀이 그려지는 거리 — 지형에 아트가 정한 거리에 곱한다 (배)")]
            [SerializeField, Range(0.1f, 1f)] private float grassDistanceScale = 0.6f;
            [Tooltip("풀 밀도 — 지형에 아트가 정한 밀도에 곱한다 (배)")]
            [SerializeField, Range(0.1f, 1f)] private float grassDensityScale = 0.75f;

            [Header("반사 · 창밖")]
            [Tooltip("호수 반사 — 켜도 호수가 화면에 보일 때만 그린다 (끄면 반사 프로브)")]
            [SerializeField] private bool lakeReflection = true;
            [Tooltip("가게 창밖 풍경 해상도 — 화면 해상도에 곱한다 (배)")]
            [SerializeField, Range(0.25f, 1f)] private float windowViewScale = 0.4f;
            [Tooltip("가게 창밖 풍경에도 그림자를 그린다")]
            [SerializeField] private bool windowViewShadows;

            [Header("화면")]
            [Tooltip("3D 화면 해상도 배율 — UI는 그대로 (배)")]
            [SerializeField, Range(0.5f, 1f)] private float renderScale = 1f;
            [Tooltip("가장자리 계단 현상 줄임 (SMAA) — 울타리 · 가는 나뭇가지가 덜 깨진다")]
            [SerializeField] private bool smaa;

            public string DisplayName => displayName;
            public float ShadowDistance => shadowDistance;
            public int ShadowResolution => shadowResolution;
            public int ShadowCascades => shadowCascades;
            public float GrassDistanceScale => grassDistanceScale;
            public float GrassDensityScale => grassDensityScale;
            public bool LakeReflection => lakeReflection;
            public float WindowViewScale => windowViewScale;
            public bool WindowViewShadows => windowViewShadows;
            public float RenderScale => renderScale;
            public bool Smaa => smaa;
        }

        [Tooltip("품질 단계 — 가벼운 것부터 (설정 화면 순서). 설정 값은 이 목록의 번호")]
        [SerializeField] private List<Tier> tiers = new List<Tier>();
        [Tooltip("설정을 처음 열 때 고른 단계 번호 (0부터)")]
        [SerializeField, Min(0)] private int defaultTier = 1;

        public IReadOnlyList<Tier> Tiers => tiers;
        public int DefaultTier => tiers.Count == 0 ? 0 : Mathf.Clamp(defaultTier, 0, tiers.Count - 1);

        /// <summary>번호의 단계 (범위 밖이면 가장 가까운 단계, 목록이 비면 null).</summary>
        public Tier Get(int index) => tiers.Count == 0 ? null : tiers[Mathf.Clamp(index, 0, tiers.Count - 1)];
    }
}
