using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Root veins (2026-10-27, user: "redesign the cheap-looking ground" -> the trees' glowing vessels continue into the
/// soil). For every tree under <see cref="treeRoots"/> a few roots wander outward from the trunk over the ground and
/// fork; they are rasterised into one texture over <see cref="areaCenter"/> +- <see cref="areaHalfSize"/>:
/// R = vein (anti-aliased, tapering), G = distance along the root from the trunk / <see cref="maxLength"/> (pulse phase).
/// DSEWA/ToonTerrain reads it through the globals _DSEWA_RootVeins / _DSEWA_RootVeinRect and lets waves run along the
/// roots into the tree. Bake from the context menu; the texture is saved as an asset and pushed on enable.
/// All numbers are my own values.
/// </summary>
[ExecuteAlways]
public class RootVeinBaker : MonoBehaviour
{
    public Transform[] treeRoots = System.Array.Empty<Transform>();
    public Vector2 areaCenter = Vector2.zero;
    public float areaHalfSize = 110f;
    [Range(256, 4096)] public int resolution = 2048;
    public Texture2D baked;

    [Header("Roots")]
    public Vector2Int rootsPerTree = new Vector2Int(4, 7);
    public float maxLength = 11f;
    public Vector2 startWidth = new Vector2(0.12f, 0.2f);
    public float endWidth = 0.03f;
    [Range(0f, 1f)] public float branchChance = 0.45f;
    public float stepLength = 0.35f;
    [Tooltip("Heading wobble per step (degrees)")]
    public float wander = 18f;
    public int seed = 3;

    const string AssetPath = "Assets/Art/Environment/DeepBlue/tex_env_ground_root_veins.asset";
    static readonly int TexId = Shader.PropertyToID("_DSEWA_RootVeins");
    static readonly int RectId = Shader.PropertyToID("_DSEWA_RootVeinRect");

    void OnEnable() => Push();
    void OnDisable() => Shader.SetGlobalVector(RectId, Vector4.zero);

    void Push()
    {
        if (baked == null) { Shader.SetGlobalVector(RectId, Vector4.zero); return; }
        Shader.SetGlobalTexture(TexId, baked);
        float s = areaHalfSize * 2f;
        Shader.SetGlobalVector(RectId, new Vector4(areaCenter.x - areaHalfSize, areaCenter.y - areaHalfSize, 1f / s, 1f / s));
    }

    struct Seg { public Vector2 a, b; public float wa, wb, da, db; }

    [ContextMenu("Bake")]
    public void Bake()
    {
        var rnd = new System.Random(seed);
        float R() => (float)rnd.NextDouble();
        var segs = new List<Seg>();
        foreach (var root in treeRoots)
        {
            if (root == null) continue;
            foreach (Transform tree in root)
            {
                if (!tree.gameObject.activeInHierarchy) continue;
                var c = new Vector2(tree.position.x, tree.position.z);
                int count = rnd.Next(rootsPerTree.x, rootsPerTree.y + 1);
                float a0 = R() * 360f;
                for (int i = 0; i < count; i++)
                {
                    float ang = a0 + i * 360f / count + (R() - 0.5f) * 30f;
                    float w = Mathf.Lerp(startWidth.x, startWidth.y, R());
                    Grow(segs, c + Dir(ang) * 0.35f, ang, w, 0f, maxLength * Mathf.Lerp(0.55f, 1f, R()), rnd, 0);
                }
            }
        }

        int N = resolution;
        var vein = new float[N * N];
        var dist = new float[N * N];
        float minX = areaCenter.x - areaHalfSize, minZ = areaCenter.y - areaHalfSize, texel = areaHalfSize * 2f / N;
        foreach (var s in segs)
        {
            float wMax = Mathf.Max(s.wa, s.wb) + texel * 1.5f;
            int x0 = Mathf.Max(0, (int)((Mathf.Min(s.a.x, s.b.x) - wMax - minX) / texel)), x1 = Mathf.Min(N - 1, (int)((Mathf.Max(s.a.x, s.b.x) + wMax - minX) / texel) + 1);
            int y0 = Mathf.Max(0, (int)((Mathf.Min(s.a.y, s.b.y) - wMax - minZ) / texel)), y1 = Mathf.Min(N - 1, (int)((Mathf.Max(s.a.y, s.b.y) + wMax - minZ) / texel) + 1);
            var ab = s.b - s.a; float len2 = Mathf.Max(ab.sqrMagnitude, 1e-8f);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(minX + (x + 0.5f) * texel, minZ + (y + 0.5f) * texel);
                    float t = Mathf.Clamp01(Vector2.Dot(p - s.a, ab) / len2);
                    float d = (p - (s.a + ab * t)).magnitude;
                    float hw = Mathf.Lerp(s.wa, s.wb, t) * 0.5f;
                    float v = Mathf.Clamp01((hw - d) / texel + 0.5f);
                    int k = y * N + x;
                    if (v > vein[k]) { vein[k] = v; dist[k] = Mathf.Lerp(s.da, s.db, t); }
                }
        }
        var px = new Color32[N * N];
        for (int k = 0; k < px.Length; k++)
            px[k] = new Color32((byte)(vein[k] * 255f), (byte)(Mathf.Clamp01(dist[k] / maxLength) * 255f), 0, 255);
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { name = "tex_env_ground_root_veins", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.SetPixels32(px);
        tex.Apply(true, false);
#if UNITY_EDITOR
        var old = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
        if (old != null) { EditorUtility.CopySerialized(tex, old); DestroyImmediate(tex); tex = old; EditorUtility.SetDirty(old); }
        else AssetDatabase.CreateAsset(tex, AssetPath);
        AssetDatabase.SaveAssets();
        baked = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
        EditorUtility.SetDirty(this);
#else
        baked = tex;
#endif
        Push();
        Debug.Log($"[RootVeinBaker] {segs.Count} segments baked", this);
    }

    static Vector2 Dir(float deg) { float r = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)); }

    void Grow(List<Seg> segs, Vector2 p, float ang, float w, float d, float len, System.Random rnd, int depth)
    {
        float R() => (float)rnd.NextDouble();
        float travelled = 0f;
        float w0 = w;
        while (travelled < len)
        {
            ang += (R() - 0.5f) * 2f * wander;
            var q = p + Dir(ang) * stepLength;
            float k0 = travelled / len, k1 = Mathf.Min(1f, (travelled + stepLength) / len);
            float wa = Mathf.Lerp(w0, endWidth, k0), wb = Mathf.Lerp(w0, endWidth, k1);
            segs.Add(new Seg { a = p, b = q, wa = wa, wb = wb, da = d + travelled, db = d + travelled + stepLength });
            if (depth < 2 && R() < branchChance * stepLength / 3f)
                Grow(segs, q, ang + (R() < 0.5f ? -1f : 1f) * (30f + R() * 25f), wb * 0.75f, d + travelled, (len - travelled) * Mathf.Lerp(0.4f, 0.8f, R()), rnd, depth + 1);
            p = q;
            travelled += stepLength;
        }
    }
}
