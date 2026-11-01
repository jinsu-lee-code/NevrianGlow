using UnityEditor;
using UnityEngine;

/// <summary>
/// Field site ground (2026-11-01, dev handoff 1-9): flattens the terrain under expansion fields 4 and 5, where it rose
/// through the soil. Each area is levelled to the field's height (FieldSite y in Farm.unity) and eases back into the
/// surrounding ground. Writes TD_LookDev_DeepBlue, so open BackGroundScene first. Safe to run again. Numbers are mine.
/// </summary>
public static class FieldSiteFlattenSetup
{
    const float FlatMargin = 1f;    // m beyond the fence
    const float BlendWidth = 3f;    // m of slope back to the original ground

    struct Site
    {
        public Rect area; public float height;
        public Site(float x, float z, float width, float depth, float height)
        {
            area = new Rect(x, z, width, depth); this.height = height;
        }
    }

    // ground rect of the field and fence, x z min, world metres
    static readonly Site[] Sites =
    {
        new Site(15f, -38.8f, 22f, 12f, 0.03216431f),
        new Site(-38.8f, -51f, 12f, 22f, 0.281854f),
    };

    [MenuItem("Glow Art/Flatten Field Sites")]
    public static void Flatten()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null) { Debug.LogError("[FieldSiteFlattenSetup] no terrain: open BackGroundScene first"); return; }
        var data = terrain.terrainData;
        int res = data.heightmapResolution;
        var heights = data.GetHeights(0, 0, res, res);
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        int changed = 0;

        foreach (var site in Sites)
        {
            float target = (site.height - origin.y) / size.y;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float wx = origin.x + x / (res - 1f) * size.x;
                float wz = origin.z + y / (res - 1f) * size.z;
                float dx = Mathf.Max(site.area.xMin - wx, 0f, wx - site.area.xMax);
                float dz = Mathf.Max(site.area.yMin - wz, 0f, wz - site.area.yMax);
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance >= FlatMargin + BlendWidth) continue;
                float blend = 1f - Mathf.SmoothStep(0f, 1f, (distance - FlatMargin) / BlendWidth);
                float result = Mathf.Lerp(heights[y, x], target, blend);
                if (Mathf.Abs(result - heights[y, x]) > 1e-6f) changed++;
                heights[y, x] = result;
            }
        }

        data.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        Debug.Log($"[FieldSiteFlattenSetup] {changed} height samples changed");
    }
}
