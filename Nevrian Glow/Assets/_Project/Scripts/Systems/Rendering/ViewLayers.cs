using System.Collections.Generic;
using NevrianGlowFarm.Data;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NevrianGlowFarm.Systems.Rendering
{
    /// <summary>
    /// 근경 · 중경 · 원경 배경 화면 (2026-10-31). 아트 화면 효과(붓 터치 · 거리 안개 · 초점 흐림)의 '거리' 값만 실행 중에 바꾼다 —
    /// 아트 재질 · 볼륨 · 렌더러 파일은 그대로 (재질은 실행 중 사본을 끼우고 끝나면 원본을 되돌림, 초점 흐림은 위에 얹는 볼륨).
    /// 아트가 효과를 끄면(초점 흐림 모드 · 효과 칸) 그대로 꺼진 채다 — 켜고 끄는 것은 덮어쓰지 않는다.
    /// </summary>
    public class ViewLayers : MonoBehaviour
    {
        // 아트 셰이더(Hidden/DSEWA/PaintBrush · AerialPerspective)의 거리 칸
        private static readonly int BrushNear = Shader.PropertyToID("_NearStrength");
        private static readonly int BrushFar = Shader.PropertyToID("_FarStrength");
        private static readonly int BrushFadeStart = Shader.PropertyToID("_FadeStart");
        private static readonly int BrushFadeEnd = Shader.PropertyToID("_FadeEnd");
        private static readonly int HazeStart = Shader.PropertyToID("_HazeStart");
        private static readonly int HazeEnd = Shader.PropertyToID("_HazeEnd");
        private static readonly int HazeDetailKeep = Shader.PropertyToID("_DetailKeep");

        [Header("참조")]
        [Tooltip("거리 구간 값 (Data/Stats/ViewLayers)")]
        [SerializeField] private ViewLayersData layers;
        [Tooltip("게임 렌더러 (Settings/PC_Renderer) — 아트 화면 효과가 달려 있다")]
        [SerializeField] private UniversalRendererData rendererData;
        [Tooltip("아트 붓 터치 재질 (Art/Environment/Materials_PP/mat_pp_paint_brush) — 효과를 찾는 데만 쓴다")]
        [SerializeField] private Material paintBrushMaterial;
        [Tooltip("아트 거리 안개 재질 (Art/Environment/Materials_PP/mat_pp_aerial_perspective) — 효과를 찾는 데만 쓴다")]
        [SerializeField] private Material aerialMaterial;

        [Header("조정값")]
        [Tooltip("초점 흐림을 얹는 볼륨 우선순위 — 아트 배경 볼륨(2)보다 높고 가게 실내 볼륨(10)보다 낮게")]
        [SerializeField] private float volumePriority = 5f;

        [Header("디버그")]
        [Tooltip("거리 값을 바꾼 아트 화면 효과 수 (읽기 전용)")]
        [SerializeField] private int swappedPasses;

        private readonly List<(FullScreenPassRendererFeature pass, Material original)> swapped = new List<(FullScreenPassRendererFeature, Material)>();
        private Volume volume;
        private VolumeProfile profile;

        // 켜기 · 끄기에 맞춰 끼우고 되돌린다 — 실행 중 스크립트 다시 불러오기에도 렌더러에 사본이 남지 않게
        private void OnEnable()
        {
            if (layers == null || rendererData == null)
            {
                Debug.LogError("[ViewLayers] 참조가 비어 있음 — 인스펙터의 참조 항목 확인", this);
                enabled = false;
                return;
            }
            SwapPasses();
            EnableBlurVolume();
        }

        private void OnDisable()
        {
            foreach (var (pass, original) in swapped)
            {
                if (pass == null)
                    continue;
                var copy = pass.passMaterial;
                pass.passMaterial = original;
                if (copy != null && copy != original)
                    Destroy(copy);
            }
            swapped.Clear();
            swappedPasses = 0;
            if (volume != null)
                volume.enabled = false;
        }

        private void OnDestroy()
        {
            if (profile != null)
                Destroy(profile);
        }

        private void SwapPasses()
        {
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (!(feature is FullScreenPassRendererFeature pass) || pass.passMaterial == null)
                    continue;
                if (pass.passMaterial == paintBrushMaterial)
                    Swap(pass, ApplyBrush);
                else if (pass.passMaterial == aerialMaterial)
                    Swap(pass, ApplyHaze);
            }
            swappedPasses = swapped.Count;
        }

        private void Swap(FullScreenPassRendererFeature pass, System.Action<Material> apply)
        {
            var original = pass.passMaterial;
            var copy = new Material(original) { name = original.name + " (ViewLayers)", hideFlags = HideFlags.DontSave };
            apply(copy);
            swapped.Add((pass, original));
            pass.passMaterial = copy;
        }

        private void ApplyBrush(Material m)
        {
            m.SetFloat(BrushNear, layers.BrushStrength);
            m.SetFloat(BrushFar, layers.BrushFarStrength);
            m.SetFloat(BrushFadeStart, layers.NearEnd);
            m.SetFloat(BrushFadeEnd, Mathf.Max(layers.BrushEnd, layers.NearEnd + 0.01f));
        }

        private void ApplyHaze(Material m)
        {
            m.SetFloat(HazeStart, layers.HazeStart);
            m.SetFloat(HazeEnd, Mathf.Max(layers.HazeEnd, layers.HazeStart + 0.01f));
            m.SetFloat(HazeDetailKeep, layers.HazeDetailKeep);
        }

        // 초점 흐림: 거리 · 크기만 얹는다 (모드는 아트 볼륨 값 — 아트가 끄면 꺼진 채, 가게 안은 실내 볼륨이 끈다)
        private void EnableBlurVolume()
        {
            if (volume == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.hideFlags = HideFlags.DontSave;
                var blur = profile.Add<DepthOfField>();
                blur.gaussianStart.Override(layers.BlurStart);
                blur.gaussianEnd.Override(Mathf.Max(layers.BlurEnd, layers.BlurStart + 0.01f));
                blur.gaussianMaxRadius.Override(layers.BlurRadius);
                volume = gameObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = volumePriority;
                volume.sharedProfile = profile;
            }
            volume.enabled = true;
        }
    }
}
