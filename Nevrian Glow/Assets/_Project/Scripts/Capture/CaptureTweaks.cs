#if UNITY_EDITOR
using System.Reflection;
using NevrianGlowFarm.Systems.Farm;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NevrianGlowFarm.Capture
{
    /// <summary>
    /// 영상 촬영 때만 켜는 화면 정리 (11/03). Play 중에만 바꾸고 끝나면 되돌린다 — 게임 데이터와 아트 재질은 그대로.
    /// · 다 자란 작물 위의 빛기둥(시선 유도용)을 숨긴다. 밭이 늘어나면 칸이 새로 생기므로 0.5초마다 다시 훑는다.
    /// · 별빛 빛줄기를 없애고 광량은 유지한다: 줄기 길이를 0으로 하면 빛나는 지점만 그 자리에서 밝아지고, 번짐은 블룸이 둥글게 퍼뜨린다.
    ///   빛줄기 후처리와 같은 지점(채도 높은 발광체)을 고르므로 등불 · 나무 줄기 · 나비가 그대로 빛난다.
    /// · 가만히 보면 빛이 사라지는 눈 피로 연출(GlareFatigue)을 끈다 — 천천히 움직이는 장면에서 광량이 오르내린다.
    /// </summary>
    public sealed class CaptureTweaks : MonoBehaviour
    {
        private const string GlareMaterial = "Assets/Art/Environment/Materials_PP/mat_pp_star_glare.mat";
        private const float GlowBoost = 4.5f;
        private const float GlowNearStart = 0.5f; // 가까운 빛(나비 · 밭 작물)도 빛나게 (m)
        private const float GlowNearFull = 4f;
        private const float ShopZoneX = 300f; // 가게는 x 500 근처에 떨어져 있다 (m)

        private static readonly FieldInfo BeamHeight = typeof(FarmCellView).GetField("beamHeight", BindingFlags.NonPublic | BindingFlags.Instance);

        private float next;
        private Material glare;
        private float glareIntensity, glareLength, glareRays, glareNearNo, glareNearFull;
        private VolumeProfile profile;

        public static void Ensure()
        {
            if (FindFirstObjectByType<CaptureTweaks>() != null)
                return;
            var go = new GameObject("CaptureTweaks");
            DontDestroyOnLoad(go);
            go.AddComponent<CaptureTweaks>();
        }

        private void Awake()
        {
            foreach (var fatigue in FindObjectsByType<GlareFatigue>(FindObjectsSortMode.None))
                fatigue.enabled = false;
            glare = AssetDatabase.LoadAssetAtPath<Material>(GlareMaterial);
            if (glare != null)
            {
                glareIntensity = glare.GetFloat("_Intensity");
                glareLength = glare.GetFloat("_Length");
                glareRays = glare.GetFloat("_Rays");
                glareNearNo = glare.GetFloat("_NearNoRays");
                glareNearFull = glare.GetFloat("_NearFullRays");
                glare.SetFloat("_Intensity", GlowBoost);
                glare.SetFloat("_Length", 0f);
                glare.SetFloat("_Rays", 1f);
                glare.SetFloat("_NearNoRays", GlowNearStart);
                glare.SetFloat("_NearFullRays", GlowNearFull);
            }
            // 야외 볼륨(우선순위 2)보다 높고 가게 안 볼륨(10)보다 낮다 — 가게 안 화면은 게임 그대로
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.DontSave;
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.25f);
            bloom.intensity.Override(1.5f);
            bloom.scatter.Override(0.66f);
            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 8f;
            volume.sharedProfile = profile;
        }

        private void OnDestroy()
        {
            if (profile != null)
                Destroy(profile);
            if (glare == null)
                return;
            glare.SetFloat("_Intensity", glareIntensity);
            glare.SetFloat("_Length", glareLength);
            glare.SetFloat("_Rays", glareRays);
            glare.SetFloat("_NearNoRays", glareNearNo);
            glare.SetFloat("_NearFullRays", glareNearFull);
        }

        /// <summary>가까운 빛을 어디부터 밝힐지 (m). 1인칭 플레이는 눈앞의 수확 빛이 터지지 않게 멀리부터.</summary>
        public static void SetGlowNear(float start, float full)
        {
            var tweaks = FindFirstObjectByType<CaptureTweaks>();
            if (tweaks == null || tweaks.glare == null)
                return;
            tweaks.glare.SetFloat("_NearNoRays", start);
            tweaks.glare.SetFloat("_NearFullRays", full);
        }

        private void LateUpdate()
        {
            // 가게 안은 볼륨 블룸이 아주 강해서(ShopInteriorVolume) 여기에 더 밝히면 조명이 하얗게 터진다 — 가게 안은 게임 블룸만
            if (glare == null || Camera.main == null)
                return;
            float wanted = Camera.main.transform.position.x > ShopZoneX ? 0f : GlowBoost;
            if (!Mathf.Approximately(glare.GetFloat("_Intensity"), wanted))
                glare.SetFloat("_Intensity", wanted);
        }

        private void Update()
        {
            if (Time.unscaledTime < next)
                return;
            next = Time.unscaledTime + 0.5f;
            foreach (var view in FindObjectsByType<FarmCellView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                BeamHeight.SetValue(view, 0f);
                var beam = view.transform.Find("ReadyBeam");
                if (beam != null && beam.gameObject.activeSelf)
                    beam.gameObject.SetActive(false);
            }
        }
    }
}
#endif
