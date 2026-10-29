using System.Collections.Generic;
using NevrianGlowFarm.Core;
using NevrianGlowFarm.Data;
using NevrianGlowFarm.Logic.Settings;
using NevrianGlowFarm.Systems.Zones;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NevrianGlowFarm.Systems.Rendering
{
    /// <summary>
    /// 그리기 예산 (최적화 2026-10-30). 측정에서 무거웠던 것을 실행 중에만 줄인다 — 아트 씬 · 스크립트 · URP 에셋 파일은 그대로.
    /// · 호수 평면 반사(아트 PlanarReflection)는 장면 전체를 그림자까지 한 번 더 그린다 → 호수가 카메라 화면 안에 있을 때만 켠다 (가게 안에서는 늘 꺼짐).
    /// · 지역마다 카메라 먼 거리(ZoneProfileData) — 가게 안에서 500 m 밖 농장 배경을 벽 너머로 그리지 않게.
    /// · 그래픽 품질 단계(설정 화면, GraphicsQualityData) — 해 그림자 · 풀 거리 · 밀도 · 가게 창밖 풍경 · 3D 해상도 배율.
    /// URP 에셋 값은 끝날 때 되돌린다 (에디터에서 아트가 보는 화면이 바뀌지 않게).
    /// </summary>
    public class RenderBudget : MonoBehaviour
    {
        // 아트 ShopWindowView가 만드는 숨은 카메라 — 그림자를 끌 칸이 밖에 없어 이름으로 찾는다 (아트 스크립트 그대로)
        private const string WindowCameraName = "ShopWindowView_Camera";

        [Header("참조")]
        [Tooltip("그래픽 품질 단계 (Data/Stats/GraphicsQuality)")]
        [SerializeField] private GraphicsQualityData quality;
        [Tooltip("플레이어 시점 카메라 (Core Player/Main Camera)")]
        [SerializeField] private Camera viewCamera;

        [Header("조정값")]
        [Tooltip("호수가 화면에서 벗어난 뒤 반사를 끄기까지 기다리는 시간 — 고개를 잠깐 돌릴 때마다 반사 텍스처를 다시 만들지 않게 (초)")]
        [SerializeField, Min(0f)] private float reflectionOffDelay = 1f;
        [Tooltip("가게 창밖 풍경 카메라를 찾는 간격 — 창밖 풍경이 방에 들어온 뒤에 카메라를 만든다 (초)")]
        [SerializeField, Min(0.1f)] private float windowSearchInterval = 0.5f;

        [Header("디버그")]
        [Tooltip("적용한 품질 단계 (읽기 전용)")]
        [SerializeField] private string appliedTier;
        [Tooltip("호수 반사를 그리는 중인지 (읽기 전용)")]
        [SerializeField] private bool lakeReflectionOn;
        [Tooltip("지금 카메라 먼 거리 (읽기 전용, m)")]
        [SerializeField] private float farClip;

        private UniversalRenderPipelineAsset pipeline;
        private float originalShadowDistance;
        private int originalShadowResolution;
        private int originalCascades;
        private float originalRenderScale;
        private float defaultFarClip;

        private GraphicsQualityData.Tier tier;
        private readonly Dictionary<Terrain, Vector2> authoredGrass = new Dictionary<Terrain, Vector2>();
        private readonly Plane[] frustum = new Plane[6];

        private PlanarReflection lake;
        private Bounds lakeBounds;
        private bool lakeBoundsKnown;
        private float lakeHiddenFor;

        private ShopWindowView window;
        private UniversalAdditionalCameraData windowCamera;
        private float windowSearchTimer;

        private void Awake()
        {
            if (quality == null || viewCamera == null)
            {
                Debug.LogError("[RenderBudget] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null)
            {
                originalShadowDistance = pipeline.shadowDistance;
                originalShadowResolution = pipeline.mainLightShadowmapResolution;
                originalCascades = pipeline.shadowCascadeCount;
                originalRenderScale = pipeline.renderScale;
            }
            defaultFarClip = viewCamera.farClipPlane;
            farClip = defaultFarClip;
        }

        private void OnEnable()
        {
            SettingsStore.Changed += OnSettingsChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SettingsStore.Changed -= OnSettingsChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start()
        {
            if (ZoneTransition.Instance != null)
            {
                ZoneTransition.Instance.ZoneChanged += OnZoneChanged;
                if (ZoneTransition.Instance.Current != null)
                    OnZoneChanged(ZoneTransition.Instance.Current);
            }
            FindArtPieces();
            ApplyQuality(SettingsStore.Current);
        }

        private void OnDestroy()
        {
            if (ZoneTransition.Instance != null)
                ZoneTransition.Instance.ZoneChanged -= OnZoneChanged;
            if (lake != null && !lake.enabled)
                lake.enabled = true;
            // 실행 중에만 바꾼 URP 에셋 값을 되돌린다 (에디터 플레이 후 아트 화면 그대로)
            if (pipeline != null)
            {
                pipeline.shadowDistance = originalShadowDistance;
                pipeline.mainLightShadowmapResolution = originalShadowResolution;
                pipeline.shadowCascadeCount = originalCascades;
                pipeline.renderScale = originalRenderScale;
            }
        }

        private void LateUpdate()
        {
            UpdateLakeReflection();
            UpdateWindowCamera();
        }

        // ── 품질 단계 ──

        private void OnSettingsChanged(GameSettings settings) => ApplyQuality(settings);

        private void ApplyQuality(GameSettings settings)
        {
            tier = quality.Get(settings.QualityIndex(quality.Tiers.Count, quality.DefaultTier));
            if (tier == null)
                return;
            appliedTier = tier.DisplayName;
            if (pipeline != null)
            {
                pipeline.shadowDistance = tier.ShadowDistance;
                pipeline.mainLightShadowmapResolution = tier.ShadowResolution;
                pipeline.shadowCascadeCount = tier.ShadowCascades;
                pipeline.renderScale = tier.RenderScale;
            }
            ApplyGrass();
            ApplyWindow();
        }

        // 풀 거리 · 밀도 = 지형마다 아트가 정한 값 × 단계 배율 (지형이 처음 보일 때 아트 값을 기억)
        private void ApplyGrass()
        {
            if (tier == null)
                return;
            foreach (var terrain in Terrain.activeTerrains)
            {
                if (terrain == null)
                    continue;
                if (!authoredGrass.TryGetValue(terrain, out var authored))
                {
                    authored = new Vector2(terrain.detailObjectDistance, terrain.detailObjectDensity);
                    authoredGrass[terrain] = authored;
                }
                terrain.detailObjectDistance = authored.x * tier.GrassDistanceScale;
                terrain.detailObjectDensity = authored.y * tier.GrassDensityScale;
            }
        }

        private void ApplyWindow()
        {
            if (tier == null || window == null)
                return;
            window.resolutionScale = tier.WindowViewScale;
            if (windowCamera != null)
                windowCamera.renderShadows = tier.WindowViewShadows;
        }

        // ── 지역 ──

        private void OnZoneChanged(ZoneProfileData zone)
        {
            farClip = zone != null && zone.CameraFarClip > 0f ? zone.CameraFarClip : defaultFarClip;
            viewCamera.farClipPlane = farClip;
        }

        // 배경 씬 · 가게 씬은 Core 뒤에 Additive로 올라온다 — 올라올 때마다 아트 부품과 새 지형을 다시 찾는다
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FindArtPieces();
            ApplyGrass();
            ApplyWindow();
        }

        private void FindArtPieces()
        {
            if (lake == null)
            {
                lake = FindFirstObjectByType<PlanarReflection>();
                lakeBoundsKnown = false;
                // 물 표면(메시)만 — 안개 · 거품 파티클은 처음 프레임에 상자가 원점 근처에 있어 농장 어디서나 '보인다'가 된다
                if (lake != null)
                    foreach (var r in lake.GetComponentsInChildren<MeshRenderer>())
                    {
                        if (!lakeBoundsKnown)
                            lakeBounds = r.bounds;
                        else
                            lakeBounds.Encapsulate(r.bounds);
                        lakeBoundsKnown = true;
                    }
            }
            if (window == null)
            {
                window = FindFirstObjectByType<ShopWindowView>();
                windowCamera = null;
            }
        }

        // ── 호수 반사 ──

        private void UpdateLakeReflection()
        {
            if (lake == null || tier == null)
                return;
            bool wanted = tier.LakeReflection && LakeOnScreen();
            if (wanted)
            {
                lakeHiddenFor = 0f;
                if (!lake.enabled)
                    lake.enabled = true;
            }
            else if (lake.enabled)
            {
                lakeHiddenFor += Time.unscaledDeltaTime;
                if (!tier.LakeReflection || lakeHiddenFor >= reflectionOffDelay)
                    lake.enabled = false;
            }
            lakeReflectionOn = lake.enabled;
        }

        // 호수 물 표면(메시를 감싸는 상자)이 카메라 시야(먼 거리 포함) 안에 있는지 — 물 메시가 없으면 늘 보인다고 본다
        private bool LakeOnScreen()
        {
            if (!lakeBoundsKnown)
                return true;
            GeometryUtility.CalculateFrustumPlanes(viewCamera, frustum);
            return GeometryUtility.TestPlanesAABB(frustum, lakeBounds);
        }

        // ── 가게 창밖 풍경 ──

        // 창밖 풍경은 플레이어가 가게 방 안에 들어온 뒤에 카메라를 만든다 → 방 안에 있는 동안만 가끔 찾아 그림자 설정을 넣는다
        private void UpdateWindowCamera()
        {
            if (window == null || windowCamera != null || window.room == null || !window.isActiveAndEnabled)
                return;
            if (!window.room.bounds.Contains(viewCamera.transform.position))
                return;
            windowSearchTimer -= Time.unscaledDeltaTime;
            if (windowSearchTimer > 0f)
                return;
            windowSearchTimer = windowSearchInterval;
            foreach (var cam in Resources.FindObjectsOfTypeAll<Camera>())
                if (cam != null && cam.name == WindowCameraName && cam.TryGetComponent(out windowCamera))
                    break;
            ApplyWindow();
        }
    }
}
