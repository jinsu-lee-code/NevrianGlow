using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Clears the terrain grass wherever a farm cell sits (2026-10-28, user: "grass should vanish by itself where a plot
/// is placed"). The farm cells live in the dev scene and appear at runtime (more when plots are added), so this
/// watches the FarmGrid's children by type name (no code dependency on the dev assembly), draws their footprints into
/// a small world-space mask and publishes it for DSEWA/ToonFoliage:
///   _DSEWA_GrassClear (R = 1 cleared)   _DSEWA_GrassClearRect (minX, minZ, 1/sizeX, 1/sizeZ)
/// Grass whose root falls inside shrinks to nothing, fading over Edge metres so the bed has a soft border.
/// Extra Roots: any other objects whose renderer footprints should clear grass too.
/// All numbers are my own values.
/// </summary>
[ExecuteAlways]
public class GrassClearZones : MonoBehaviour
{
    [Tooltip("Component type name of the farm grid; its children with the cell component are the cleared cells")]
    public string gridTypeName = "FarmGrid";
    public string cellTypeName = "FarmCellView";
    [Tooltip("Child renderer of a cell that gives its footprint (falls back to cellSize)")]
    public string footprintChild = "Soil";
    public float cellSize = 1.6f;
    [Tooltip("Extra clear margin round each footprint (m)")]
    public float padding = 0.1f;
    [Tooltip("Fade width outside the footprint (m)")]
    public float edge = 0.35f;
    [Tooltip("Mask resolution (m per texel)")]
    public float texel = 0.2f;
    public Transform[] extraRoots = new Transform[0];
    [Tooltip("Seconds between checks for new / moved cells")]
    public float checkInterval = 0.5f;

    static readonly int TexId = Shader.PropertyToID("_DSEWA_GrassClear");
    static readonly int RectId = Shader.PropertyToID("_DSEWA_GrassClearRect");

    Texture2D mask;
    Transform grid;
    float nextCheck;
    int lastHash;
    readonly List<Rect> rects = new List<Rect>();

    void OnEnable() { lastHash = 0; nextCheck = 0; }

    void OnDisable()
    {
        Shader.SetGlobalVector(RectId, Vector4.zero);
        if (mask != null) { DestroyImmediate(mask); mask = null; }
    }

    void Update()
    {
        float now = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartupAsDouble;
        if (now < nextCheck) return;
        nextCheck = now + checkInterval;

        Collect();
        if (grid == null) nextCheck = now + 2f;   // no farm loaded: look less often (the search scans every script)
        int hash = rects.Count;
        foreach (var r in rects) hash = hash * 31 + r.GetHashCode();
        if (hash == lastHash) return;
        lastHash = hash;
        Bake();
    }

    void Collect()
    {
        rects.Clear();
        if (grid == null) grid = FindByTypeName(gridTypeName);
        if (grid != null)
            foreach (Transform cell in grid)
                if (cell.gameObject.activeInHierarchy && HasComponent(cell, cellTypeName))
                    rects.Add(Footprint(cell));
        foreach (var root in extraRoots)
        {
            if (root == null) continue;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                if (!(r is ParticleSystemRenderer))
                    rects.Add(new Rect(r.bounds.min.x, r.bounds.min.z, r.bounds.size.x, r.bounds.size.z));
        }
    }

    Rect Footprint(Transform cell)
    {
        var soil = cell.Find(footprintChild);
        var rend = soil != null ? soil.GetComponent<Renderer>() : null;
        if (rend != null)
            return new Rect(rend.bounds.min.x, rend.bounds.min.z, rend.bounds.size.x, rend.bounds.size.z);
        var p = cell.position;
        return new Rect(p.x - cellSize * 0.5f, p.z - cellSize * 0.5f, cellSize, cellSize);
    }

    void Bake()
    {
        if (rects.Count == 0) { Shader.SetGlobalVector(RectId, Vector4.zero); return; }
        float m = padding + edge + texel;
        Rect all = rects[0];
        foreach (var r in rects) all = Rect.MinMaxRect(Mathf.Min(all.xMin, r.xMin), Mathf.Min(all.yMin, r.yMin), Mathf.Max(all.xMax, r.xMax), Mathf.Max(all.yMax, r.yMax));
        all = Rect.MinMaxRect(all.xMin - m, all.yMin - m, all.xMax + m, all.yMax + m);
        float t = Mathf.Max(texel, Mathf.Max(all.width, all.height) / 1024f);
        int w = Mathf.Max(4, Mathf.CeilToInt(all.width / t)), h = Mathf.Max(4, Mathf.CeilToInt(all.height / t));

        if (mask == null || mask.width != w || mask.height != h)
        {
            if (mask != null) DestroyImmediate(mask);
            mask = new Texture2D(w, h, TextureFormat.R8, false, true)
                { name = "GrassClearMask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        }
        var px = new byte[w * h];
        foreach (var r in rects)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((r.xMin - m - all.xMin) / t)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt((r.xMax + m - all.xMin) / t));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((r.yMin - m - all.yMin) / t)), z1 = Mathf.Min(h - 1, Mathf.CeilToInt((r.yMax + m - all.yMin) / t));
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    float wx = all.xMin + (x + 0.5f) * t, wz = all.yMin + (z + 0.5f) * t;
                    float dx = Mathf.Max(r.xMin - padding - wx, wx - r.xMax - padding, 0f);
                    float dz = Mathf.Max(r.yMin - padding - wz, wz - r.yMax - padding, 0f);
                    float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(dx * dx + dz * dz) / Mathf.Max(edge, 1e-3f));
                    byte b = (byte)Mathf.RoundToInt(k * 255f);
                    int i = z * w + x;
                    if (b > px[i]) px[i] = b;
                }
        }
        mask.SetPixelData(px, 0);
        mask.Apply(false, false);
        Shader.SetGlobalTexture(TexId, mask);
        Shader.SetGlobalVector(RectId, new Vector4(all.xMin, all.yMin, 1f / (w * t), 1f / (h * t)));
    }

    static Transform FindByTypeName(string typeName)
    {
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (mb != null && mb.GetType().Name == typeName) return mb.transform;
        return null;
    }

    static bool HasComponent(Transform t, string typeName)
    {
        foreach (var mb in t.GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == typeName) return true;
        return false;
    }
}
