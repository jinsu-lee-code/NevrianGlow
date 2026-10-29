using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Rock bedding (2026-10-27, user: "when a rock is buried, the dirt / mud round it should be pushed up following the
/// buried shape"). For every rock under <see cref="rockRoots"/> the line where its mesh cuts the terrain is measured
/// (triangle edges crossing the ground, in <see cref="bins"/> angle bins round the rock, so the outline follows the
/// real rock shape), and a soil skirt is built along it: it climbs a little up the rock, heaps into an uneven rim,
/// then sinks under the terrain so the outer edge disappears. Height is irregular round the rock (soil pushed more to
/// one side) with small clods. The terrain heightmap (0.49 m cells) is too coarse for this, hence a mesh.
/// Drawn with DSEWA/ToonBedding (ground colour from the RVT, wet mud at the rock). Meshes are merged per
/// <see cref="cellSize"/> grid cell and saved in <see cref="meshAssetPath"/>. Bake from the component's context menu.
/// All numbers are my own values.
/// </summary>
public class RockBedding : MonoBehaviour
{
    public Terrain terrain;
    [Tooltip("Roots whose rocks get a bedding (each outermost prefab instance = one rock)")]
    public Transform[] rockRoots = Array.Empty<Transform>();
    public Material material;
    [Tooltip("Project path of the asset that stores the baked meshes")]
    public string meshAssetPath = "Assets/Art/Environment/rock_bg/Models/env_rock_bg_bedding.asset";

    [Header("Which rocks")]
    [Tooltip("Rocks with a mean contact radius outside this range are skipped (pebbles, cliffs) (m)")]
    public Vector2 radiusRange = new Vector2(0.15f, 4f);

    [Header("Shape (scaled by the rock's contact radius)")]
    [Tooltip("Skirt width = radius x this, clamped")]
    public float widthPerRadius = 0.45f;
    public Vector2 widthRange = new Vector2(0.2f, 1.1f);
    [Tooltip("Rim height = radius x this, clamped")]
    public float rimPerRadius = 0.1f;
    public Vector2 rimRange = new Vector2(0.03f, 0.2f);
    [Tooltip("Where the rim peaks across the skirt (0 = at the rock, 1 = outer edge)")]
    [Range(0.05f, 0.8f)] public float rimPeak = 0.25f;
    [Tooltip("How far up the rock the soil climbs, as a fraction of the rim")]
    [Range(0f, 1.5f)] public float climb = 0.6f;
    [Tooltip("How far the inner edge goes inside the rock (m), hides the seam")]
    public float inset = 0.05f;
    [Tooltip("Outer edge depth under the terrain (m)")]
    public float sink = 0.05f;
    [Tooltip("Rim height variation round the rock (0 = even, 1 = some sides flat, others double)")]
    [Range(0f, 1f)] public float rimVariation = 0.7f;
    [Tooltip("Width variation round the rock")]
    [Range(0f, 1f)] public float widthVariation = 0.5f;
    [Tooltip("Clod bumps, fraction of the rim")]
    [Range(0f, 1f)] public float clods = 0.35f;

    [Header("Resolution")]
    [Range(12, 96)] public int bins = 48;
    [Range(2, 10)] public int rings = 5;
    public float cellSize = 32f;
    public int seed = 11;

    struct Cell { public List<Vector3> v; public List<Color> c; public List<int> t; }

    float H(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.GetPosition().y;

    [ContextMenu("Bake")]
    public void Bake()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (terrain == null) { Debug.LogWarning("[RockBedding] no terrain", this); return; }
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);

        var rocks = CollectRocks();
        var cells = new Dictionary<Vector2Int, Cell>();
        int built = 0, skipped = 0;
        foreach (var rock in rocks)
        {
            if (BuildRock(rock, cells)) built++; else skipped++;
        }

        var meshes = new List<Mesh>();
        foreach (var kv in cells)
        {
            var m = new Mesh { name = $"bedding_{kv.Key.x}_{kv.Key.y}" };
            m.indexFormat = kv.Value.v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(kv.Value.v);
            m.SetColors(kv.Value.c);
            m.SetTriangles(kv.Value.t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            meshes.Add(m);
            var go = new GameObject(m.name);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        SaveMeshes(meshes);
        Debug.Log($"[RockBedding] {built} rocks bedded, {skipped} skipped, {meshes.Count} meshes", this);
    }

    List<GameObject> CollectRocks()
    {
        var set = new List<GameObject>();
        var seen = new HashSet<GameObject>();
        foreach (var root in rockRoots)
        {
            if (root == null) continue;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.gameObject.activeInHierarchy) continue;
                GameObject r = mf.gameObject;
#if UNITY_EDITOR
                var pr = PrefabUtility.GetOutermostPrefabInstanceRoot(mf.gameObject);
                if (pr != null) r = pr;
#endif
                if (seen.Add(r)) set.Add(r);
            }
        }
        return set;
    }

    bool BuildRock(GameObject rock, Dictionary<Vector2Int, Cell> cells)
    {
        // world triangles of the rock
        var pts = new List<Vector3>();
        var tris = new List<int>();
        var bounds = new Bounds();
        bool first = true;
        foreach (var mf in rock.GetComponentsInChildren<MeshFilter>())
        {
            var m = mf.sharedMesh;
            if (m == null) continue;
            int b0 = pts.Count;
            var M = mf.transform.localToWorldMatrix;
            foreach (var v in m.vertices)
            {
                var w = M.MultiplyPoint3x4(v);
                pts.Add(w);
                if (first) { bounds = new Bounds(w, Vector3.zero); first = false; } else bounds.Encapsulate(w);
            }
            foreach (var t in m.triangles) tris.Add(b0 + t);
        }
        if (pts.Count == 0) return false;

        var f = new float[pts.Count];
        for (int i = 0; i < pts.Count; i++) f[i] = pts[i].y - H(pts[i].x, pts[i].z);

        // contact points: triangle edges crossing the ground
        var contact = new List<Vector2>();
        for (int i = 0; i < tris.Count; i += 3)
            for (int e = 0; e < 3; e++)
            {
                int a = tris[i + e], b = tris[i + (e + 1) % 3];
                if (a > b) continue;   // each shared edge once
                if ((f[a] > 0) == (f[b] > 0)) continue;
                var p = Vector3.Lerp(pts[a], pts[b], f[a] / (f[a] - f[b]));
                contact.Add(new Vector2(p.x, p.z));
            }
        if (contact.Count < 6)
        {
            // resting on the ground, not cut by it: use the lowest band of vertices as the foot
            float minY = bounds.min.y, band = Mathf.Max(0.04f, bounds.size.y * 0.1f);
            for (int i = 0; i < pts.Count; i++) if (pts[i].y < minY + band) contact.Add(new Vector2(pts[i].x, pts[i].z));
            if (contact.Count < 3) return false;
        }

        // outline: furthest contact in each angle bin round the centre
        var c = new Vector2(bounds.center.x, bounds.center.z);
        var r = new float[bins];
        foreach (var p in contact)
        {
            var d = p - c;
            int k = Mathf.FloorToInt((Mathf.Atan2(d.y, d.x) / (2 * Mathf.PI) + 0.5f) * bins) % bins;
            r[k] = Mathf.Max(r[k], d.magnitude);
        }
        if (!FillGaps(r)) return false;
        var sm = new float[bins];
        for (int k = 0; k < bins; k++) sm[k] = 0.25f * r[(k + bins - 1) % bins] + 0.5f * r[k] + 0.25f * r[(k + 1) % bins];
        r = sm;

        float meanR = 0;
        foreach (var x in r) meanR += x;
        meanR /= bins;
        if (meanR < radiusRange.x || meanR > radiusRange.y) return false;

        float width = Mathf.Clamp(meanR * widthPerRadius, widthRange.x, widthRange.y);
        float rim = Mathf.Clamp(meanR * rimPerRadius, rimRange.x, rimRange.y);
        var rnd = new System.Random(seed ^ (Mathf.RoundToInt(c.x * 73.1f) * 9176) ^ Mathf.RoundToInt(c.y * 37.7f));
        // smooth random loops round the rock (3 harmonics)
        float ph1 = (float)rnd.NextDouble() * 6.283f, ph2 = (float)rnd.NextDouble() * 6.283f, ph3 = (float)rnd.NextDouble() * 6.283f;
        float pw1 = (float)rnd.NextDouble() * 6.283f, pw2 = (float)rnd.NextDouble() * 6.283f;

        var key = new Vector2Int(Mathf.FloorToInt(c.x / cellSize), Mathf.FloorToInt(c.y / cellSize));
        if (!cells.TryGetValue(key, out var cell)) { cell = new Cell { v = new List<Vector3>(), c = new List<Color>(), t = new List<int>() }; cells[key] = cell; }
        int baseIdx = cell.v.Count;
        int rows = rings + 1;

        for (int k = 0; k < bins; k++)
        {
            float a = ((k + 0.5f) / bins - 0.5f) * 2 * Mathf.PI;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float rv = 0.55f * Mathf.Sin(a + ph1) + 0.3f * Mathf.Sin(2 * a + ph2) + 0.15f * Mathf.Sin(3 * a + ph3);   // -1..1
            float rimK = rim * Mathf.Max(0.15f, 1 + rv * rimVariation);
            float wv = 0.6f * Mathf.Sin(a + pw1) + 0.4f * Mathf.Sin(2 * a + pw2);
            float widthK = width * (1 + wv * widthVariation * 0.5f);
            for (int j = 0; j < rows; j++)
            {
                float t = (float)j / rings;
                float rad = j == 0 ? Mathf.Max(r[k] - inset, r[k] * 0.85f) : r[k] + widthK * t;
                float shape = t < rimPeak ? Mathf.Lerp(climb, 1f, t / rimPeak) : 1f - Mathf.SmoothStep(0f, 1f, (t - rimPeak) / (1f - rimPeak));
                float h = rimK * shape - sink * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, t));
                if (j > 0 && j < rings) h += rim * clods * ((float)rnd.NextDouble() - 0.5f) * 2f * (1f - t);
                var p = c + dir * rad;
                cell.v.Add(new Vector3(p.x, H(p.x, p.y) + h, p.y));
                cell.c.Add(new Color(1f - t, 0, 0, 1));
            }
        }
        for (int k = 0; k < bins; k++)
        {
            int k2 = (k + 1) % bins;
            for (int j = 0; j < rings; j++)
            {
                int a0 = baseIdx + k * rows + j, a1 = a0 + 1;
                int b0 = baseIdx + k2 * rows + j, b1 = b0 + 1;
                // angle grows counter-clockwise seen from above -> wind so the faces point up
                cell.t.Add(a0); cell.t.Add(b0); cell.t.Add(a1);
                cell.t.Add(a1); cell.t.Add(b0); cell.t.Add(b1);
            }
        }
        return true;
    }

    // empty angle bins: interpolate round the circle between the nearest filled bins
    bool FillGaps(float[] r)
    {
        int n = r.Length, filled = 0;
        foreach (var x in r) if (x > 0) filled++;
        if (filled < 3) return false;
        var src = (float[])r.Clone();
        for (int k = 0; k < n; k++)
        {
            if (src[k] > 0) continue;
            int lo = 1, hi = 1;
            while (src[(k - lo + n) % n] <= 0) lo++;
            while (src[(k + hi) % n] <= 0) hi++;
            r[k] = Mathf.Lerp(src[(k - lo + n) % n], src[(k + hi) % n], lo / (float)(lo + hi));
        }
        return true;
    }

    void SaveMeshes(List<Mesh> meshes)
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(meshAssetPath)) return;
        AssetDatabase.DeleteAsset(meshAssetPath);
        var holder = ScriptableObject.CreateInstance<RockBeddingMeshes>();
        AssetDatabase.CreateAsset(holder, meshAssetPath);
        foreach (var m in meshes) AssetDatabase.AddObjectToAsset(m, holder);
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(gameObject);
#endif
    }
}
