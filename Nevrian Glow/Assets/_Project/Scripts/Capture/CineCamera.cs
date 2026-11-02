#if UNITY_EDITOR
using System.Collections;
using NevrianGlowFarm.Systems.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NevrianGlowFarm.Capture
{
    /// <summary>
    /// 티저용 카메라 (11/03). 플레이어 카메라를 빌려 지점들을 부드럽게 잇는 곡선으로 움직인다. 화면 글자 · 손에 든 도구는 숨긴다.
    /// 지점 = 카메라 위치 + 보는 곳, 곡선은 Catmull-Rom, 시간은 처음 · 끝만 살짝 천천히.
    /// </summary>
    public sealed class CineCamera : MonoBehaviour
    {
        public struct Key
        {
            public Vector3 Position;
            public Vector3 Look;
            public Key(Vector3 position, Vector3 look)
            {
                Position = position;
                Look = look;
            }
        }

        private Camera cam;
        private Volume volume;
        private DepthOfField focus;
        private VolumeProfile profile;

        public Camera Camera => cam;

        public static CineCamera Create()
        {
            var go = new GameObject("CineCamera");
            DontDestroyOnLoad(go);
            var c = go.AddComponent<CineCamera>();
            c.Setup();
            return c;
        }

        private void Setup()
        {
            CaptureTweaks.Ensure();
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                player.enabled = false;
            cam = Camera.main;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace)
                    canvas.enabled = false;
            // 카메라 바로 앞을 지나는 빛 입자가 화면을 덮지 않게 화면 크기를 묶는다
            foreach (var r in FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (r.renderMode == ParticleSystemRenderMode.Billboard)
                    r.maxParticleSize = Mathf.Min(r.maxParticleSize, 0.035f);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.DontSave;
            focus = profile.Add<DepthOfField>();
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 9f;
            volume.sharedProfile = profile;
            ClearFocus();
        }

        /// <summary>가까운 곳에 초점 (m) — 뒤가 흐려진다. 접사 장면용.</summary>
        public void SetFocus(float start, float end, float radius = 1.5f)
        {
            focus.active = true;
            focus.mode.Override(DepthOfFieldMode.Gaussian);
            focus.gaussianStart.Override(start);
            focus.gaussianEnd.Override(end);
            focus.gaussianMaxRadius.Override(radius);
        }

        /// <summary>초점 효과를 덮어쓰지 않는다 (게임 값 그대로).</summary>
        public void ClearFocus() => focus.active = false;

        public void Place(Vector3 position, Vector3 look, float fov = -1f)
        {
            cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(look - position, Vector3.up));
            if (fov > 0f)
                cam.fieldOfView = fov;
        }

        /// <summary>지점들을 지나 seconds 동안 움직인다. easeEnds = 처음 · 끝 느려지는 정도 (0 ~ 0.5).</summary>
        public IEnumerator Fly(Key[] keys, float seconds, float fov0 = 45f, float fov1 = -1f, float easeEnds = 0.18f, float roll = 0f)
        {
            if (fov1 <= 0f)
                fov1 = fov0;
            for (float t = 0f; t <= seconds; t += Time.deltaTime)
            {
                float x = Ease(Mathf.Clamp01(t / seconds), easeEnds);
                Sample(keys, x, out var p, out var l);
                cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(l - p, Vector3.up) * Quaternion.Euler(0f, 0f, roll * Mathf.Sin(x * Mathf.PI)));
                cam.fieldOfView = Mathf.Lerp(fov0, fov1, x);
                yield return null;
            }
        }

        /// <summary>곡선의 x (0 ~ 1) 지점. 시험 촬영에서도 쓴다.</summary>
        public static void Sample(Key[] keys, float x, out Vector3 position, out Vector3 look)
        {
            int m = keys.Length - 1;
            float u = Mathf.Clamp01(x) * m;
            int k = Mathf.Min((int)u, m - 1);
            float f = u - k;
            Key a = keys[Mathf.Max(k - 1, 0)], b = keys[k], c = keys[k + 1], d = keys[Mathf.Min(k + 2, m)];
            position = CatmullRom(a.Position, b.Position, c.Position, d.Position, f);
            look = CatmullRom(a.Look, b.Look, c.Look, d.Look, f);
        }

        private static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (3f * b - a - 3f * c + d) * t3);
        }

        private static float Ease(float x, float e)
        {
            if (e <= 0f)
                return x;
            float v = 1f / (1f - e);
            if (x < e)
                return v * x * x / (2f * e);
            if (x > 1f - e)
            {
                float y = 1f - x;
                return 1f - v * y * y / (2f * e);
            }
            return v * (x - e * 0.5f);
        }

        private void OnDestroy()
        {
            if (profile != null)
                Destroy(profile);
        }
    }
}
#endif
