using UnityEngine;

/// <summary>
/// Horizon silhouettes (2026-10-30, user: "more sense of space"): rings of low ridges outside the terrain so the far
/// layer is not empty sky. Built at load (and on any inspector change in the editor), never saved - only the numbers
/// live in the scene. Each ring is a wall facing the centre: foot below the terrain edge, crest from a height profile
/// that changes with the azimuth like the eyeball planet - flat-topped mesas towards the sun (day side desert), sharp
/// ice ridges away from it (night side), rolling hills in between. Drawn with DSEWA/HorizonSilhouette
/// (uv.x = azimuth 0..1, uv.y = 0 foot .. 1 crest). All numbers are mine.
/// </summary>
[ExecuteAlways]
public class HorizonRingBuilder : MonoBehaviour
{
    [System.Serializable]
    public class Ring
    {
        public float radius = 320f;
        [Tooltip("Crest height (m above this object) for rolling hills / mesas / ice ridges")] public Vector3 heights = new Vector3(70f, 95f, 140f);
        [Tooltip("Random height swing (0..1)")] public float roughness = 0.35f;
        [Tooltip("Features round the ring (bigger = more, narrower peaks)")] public float frequency = 9f;
        public int seed = 1;
        public Material material;
    }

    public Ring[] rings = { new Ring() };
    [Tooltip("Foot of the walls (m below this object), hidden under the terrain edge")] public float footDepth = 20f;
    [Tooltip("Segments round each ring")] public int segments = 720;
    [Tooltip("World direction of the sun side (mesas); defaults to the main light")] public Light sun;

    void OnEnable() => Build();
#if UNITY_EDITOR
    // components cannot be added inside OnValidate: rebuild right after it
    void OnValidate() { UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Build(); }; }
#endif

    void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i).gameObject;
            if (c.name.StartsWith("HorizonRing_")) { if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }
        }
        Vector3 sunDir = sun != null ? -sun.transform.forward : (RenderSettings.sun != null ? -RenderSettings.sun.transform.forward : Vector3.forward);
        float sunAz = Mathf.Atan2(sunDir.x, sunDir.z);
        for (int r = 0; r < rings.Length; r++)
        {
            var ring = rings[r];
            var go = new GameObject("HorizonRing_" + r) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = MakeMesh(ring, sunAz);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ring.material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
    }

    Mesh MakeMesh(Ring ring, float sunAz)
    {
        int n = Mathf.Max(segments, 16);
        var v = new Vector3[(n + 1) * 2];
        var uv = new Vector2[(n + 1) * 2];
        var tri = new int[n * 6];
        float s = ring.seed * 17.31f;
        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / n;
            float a = t * Mathf.PI * 2f;
            // 1 = towards the sun (mesas), -1 = away (ice), 0 = sides (hills)
            float side = Mathf.Cos(a - sunAz);
            int f = Mathf.Max(2, Mathf.RoundToInt(ring.frequency));   // whole number: the noise wraps round the ring, no seam
            float x = t * f;
            float fbm = Noise(x, f, s) * 0.6f + Noise(x * 2f, f * 2, s + 5f) * 0.3f + Noise(x * 5f, f * 5, s + 9f) * 0.1f;   // 0..1
            float hills = ring.heights.x * (1f - ring.roughness + ring.roughness * fbm);
            // mesas: flat tops with steep sides - the noise pushed through a hard step
            float mesaN = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.5f, fbm));
            float mesas = ring.heights.y * (0.55f + 0.45f * mesaN);
            // ice: sharp ridges - folded noise
            float ridge = 1f - Mathf.Abs(Noise(x * 2f, f * 2, s + 3f) * 2f - 1f);
            float ice = ring.heights.z * (0.45f + 0.55f * ridge * ridge * (1f - ring.roughness * 0.5f + ring.roughness * 0.5f * fbm));
            float h = side >= 0 ? Mathf.Lerp(hills, mesas, Mathf.SmoothStep(0f, 1f, side)) : Mathf.Lerp(hills, ice, Mathf.SmoothStep(0f, 1f, -side));
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            v[i * 2] = dir * ring.radius + Vector3.down * footDepth;
            v[i * 2 + 1] = dir * ring.radius + Vector3.up * h;
            uv[i * 2] = new Vector2(t, 0f);
            uv[i * 2 + 1] = new Vector2(t, 1f);
            if (i < n)
            {
                int b = i * 2, k = i * 6;
                // wall faces the centre
                tri[k] = b; tri[k + 1] = b + 1; tri[k + 2] = b + 2;
                tri[k + 3] = b + 1; tri[k + 4] = b + 3; tri[k + 5] = b + 2;
            }
        }
        var m = new Mesh { name = "HorizonRing", hideFlags = HideFlags.DontSave };
        m.vertices = v; m.uv = uv; m.triangles = tri;
        m.RecalculateNormals();
        m.bounds = new Bounds(Vector3.zero, Vector3.one * (ring.radius * 2f + 10f) + Vector3.up * 400f);
        return m;
    }

    // smooth 1D value noise that repeats every 'period' cells (closes round the ring)
    static float Noise(float x, int period, float seed)
    {
        float i = Mathf.Floor(x), f = x - i;
        float u = f * f * (3f - 2f * f);
        return Mathf.Lerp(Hash(Mathf.Repeat(i, period) + seed), Hash(Mathf.Repeat(i + 1f, period) + seed), u);
    }
    static float Hash(float n) => Mathf.Repeat(Mathf.Sin(n * 127.1f) * 43758.5453f, 1f);
}
